#!/usr/bin/env python3
"""Reproduce the locked final evaluation without reselecting parameters or starting the web host."""
import argparse,pathlib,shutil,subprocess,hashlib,json
p=argparse.ArgumentParser();p.add_argument('--output',required=True);a=p.parse_args();evidence=pathlib.Path('evaluation/evidence/win-odds-v1');out=pathlib.Path(a.output)
if out.exists() and any(out.iterdir()):raise SystemExit('Use a new, empty output directory')
out.mkdir(parents=True,exist_ok=True)
for name in ('frozen-selection.json','frozen-selection.sha256','development-comparison.json','calibration-artifact.json'):
 shutil.copy2(evidence/name,out/name)
sha=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
subprocess.run(['dotnet','run','--project','PowerLinesWeb.Evaluation','--configuration','Release','--','--snapshot',str(evidence/'snapshot.jsonl.gz'),'--quotes',str(evidence/'qualified-quotes.json.gz'),'--manifest','evaluation/manifests/win-odds-v1.json','--output',str(out),'--phase','final','--code-sha',sha],check=True)
expected=json.loads((evidence/'reproducibility.json').read_text())
for name in ('predictions.csv','report.json','calibration.json'):
 actual=hashlib.sha256((out/name).read_bytes()).hexdigest()
 if actual!=expected['Sha256'][name]:raise SystemExit(f'Reproduction differs: {name}; compare runtime/code and report the discrepancy')
print('Locked predictions, metrics and reliability curves match the recorded SHA-256 values.')
