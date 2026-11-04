#!/usr/bin/env python3
"""Reconcile all 380 E0 2018/19 rows in a disposable DB. Never accepts a shared DB name."""
import argparse,json,pathlib,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--container',required=True);p.add_argument('--database',default='power_lines_test_reconcile');p.add_argument('--snapshot',required=True);p.add_argument('--stage',required=True);p.add_argument('--schema',required=True);p.add_argument('--output',required=True);a=p.parse_args()
assert a.database.startswith('power_lines_test_') and a.container.startswith('power-lines-win-odds-test')
def sql(s):return subprocess.check_output(['docker','exec','-i',a.container,'psql','-XqAt','-v','ON_ERROR_STOP=1','-U','postgres','-d',a.database],input=s.encode()).decode()
subprocess.run(['docker','exec',a.container,'createdb','-U','postgres',a.database],check=True)
sql(pathlib.Path(a.schema).read_text())
stage=[r for r in json.loads(pathlib.Path(a.stage).read_text()) if '/1819/E0.csv' in r['SourceUrl']];assert len(stage)==380
ids={r['ResultId'] for r in stage};source={}
for line in open(a.snapshot):
 x=json.loads(line)
 if x['Type']=='Result' and x['Data']['resultId'] in ids:source[x['Data']['resultId']]=x['Data']
def literal(s):return "'"+str(s).replace("'","''")+"'"
for r in source.values():
 columns=['resultId','division','date','homeTeam','awayTeam','fullTimeHomeGoals','fullTimeAwayGoals','fullTimeResult','homeOddsAverage','drawOddsAverage','awayOddsAverage','created']
 values=[str(r[k]) if isinstance(r[k],(int,float)) else literal(r[k]) for k in columns[:-1]]+["TIMESTAMP '2019-01-01'"]
 sql('INSERT INTO results ('+','.join('"'+k+'"' for k in columns)+') VALUES ('+','.join(values)+');')
with tempfile.TemporaryDirectory() as d:
 path=pathlib.Path(d);(path/'stage.json').write_text(json.dumps(stage))
 subprocess.run(['python3',str(pathlib.Path(__file__).with_name('build-repair-stage.py')),'--stage',str(path/'stage.json'),'--output',d],check=True)
 first=sql((path/'apply-reviewed-stage.sql').read_text());again=sql((path/'apply-reviewed-stage.sql').read_text())
 assert first.strip()=='380',first;assert again.strip()=='0',again
 assert sql("SELECT count(*) FROM results WHERE created=TIMESTAMP '2019-01-01' AND \"Revision\"=1 AND \"ScoreRevision\"=0;").strip()=='380'
 rows=json.loads(sql('SELECT json_agg(row_to_json(r)) FROM results r;'));lookup={r['resultId']:r for r in rows}
 assert all([lookup[r['ResultId']][k] for k in ('homeOddsAverage','drawOddsAverage','awayOddsAverage')]==r['After'] for r in stage)
 sql((path/'rollback-prices.sql').read_text())
 rows=json.loads(sql('SELECT json_agg(row_to_json(r)) FROM results r;'));lookup={r['resultId']:r for r in rows}
 assert all([lookup[r['ResultId']][k] for k in ('homeOddsAverage','drawOddsAverage','awayOddsAverage')]==r['Before'] for r in stage)
report={'Database':'isolated disposable test DB','Cohort':'E0 2018/19','Rows':380,'FirstApplyChanged':380,'SecondApplyChanged':0,'AfterMatchesSourceAverage':380,'OriginalCreatedPreserved':380,'ScoreRevisionUnchanged':380,'RollbackRestoredNumericPrices':380,'FullProvenanceRollback':'Requires verified pre-rollout backup; inverse SQL restores numeric triplets only.'}
pathlib.Path(a.output).write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
