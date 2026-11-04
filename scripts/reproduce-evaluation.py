#!/usr/bin/env python3
"""Reproduce locked final predictions using the recorded .NET 8 SDK/runtime, without web workers."""
import argparse,pathlib,shutil,subprocess,hashlib,json,os
p=argparse.ArgumentParser();p.add_argument('--output',required=True);p.add_argument('--container',action='store_true',help='Use the pinned .NET 8 image (automatic when the local SDK/runtime differs)');a=p.parse_args()
root=pathlib.Path(__file__).resolve().parent.parent;evidence=root/'evaluation/evidence/win-odds-v1';out=pathlib.Path(a.output).resolve()
if out.exists() and any(out.iterdir()):raise SystemExit('Use a new, empty output directory')
out.mkdir(parents=True,exist_ok=True)
for name in ('frozen-selection.json','frozen-selection.sha256','development-comparison.json','calibration-artifact.json'):shutil.copy2(evidence/name,out/name)
sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
print('Using pinned .NET 8 SDK/runtime for exact reproduction.',flush=True)
image='mcr.microsoft.com/dotnet/sdk:8.0-alpine@sha256:96a932f1623d70929c769fafcec11d8c538c511e1524491b1af352f0451f6a3f'
uid=os.getuid();gid=os.getgid();tag=f'power-lines-net8-reproduction:{uid}-{gid}'
# A real passwd entry gives the SDK/NuGet a writable user profile and named-mutex location.
dockerfile=f"FROM {image}\n"
if uid != 0:
 dockerfile+=f"RUN addgroup -g {gid} dotnet && adduser -u {uid} -G dotnet -s /bin/sh -D dotnet\nUSER dotnet\nWORKDIR /home/dotnet\n"
subprocess.run(['docker','build','--tag',tag,'-'],input=dockerfile.encode(),check=True)
script='''set -eu
mkdir /tmp/replay
cp -R /source/PowerLinesWeb /source/PowerLinesWeb.Evaluation /tmp/replay/
cp /source/Directory.Build.props /tmp/replay/
cd /tmp/replay
dotnet run --project PowerLinesWeb.Evaluation --configuration Release -- --snapshot /source/evaluation/evidence/win-odds-v1/snapshot.jsonl.gz --quotes /source/evaluation/evidence/win-odds-v1/qualified-quotes.json.gz --manifest /source/evaluation/manifests/win-odds-v1.json --output /output --phase final --code-sha "$1"
'''
command=['docker','run','--rm','-v',str(root)+':/source:ro','-v',str(out)+':/output','--entrypoint','sh',tag,'-c',script,'reproduce',sha]
subprocess.run(command,cwd=root,check=True)
expected=json.loads((evidence/'reproducibility.json').read_text())
for name in ('predictions.csv','report.json','calibration.json'):
 actual=hashlib.sha256((out/name).read_bytes()).hexdigest()
 if actual!=expected['Sha256'][name]:raise SystemExit(f'Reproduction differs: {name}; compare runtime/code and report the discrepancy')
print('Locked predictions, metrics and reliability curves match the recorded SHA-256 values.')
