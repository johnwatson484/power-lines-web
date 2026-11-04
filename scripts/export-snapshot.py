#!/usr/bin/env python3
"""Read-only, single repeatable-read snapshot. Never starts the web host."""
import argparse, hashlib, json, pathlib, subprocess, datetime
p=argparse.ArgumentParser(); p.add_argument('--container',default='power-lines-power-lines-web-postgres-1'); p.add_argument('--output',required=True); a=p.parse_args()
out=pathlib.Path(a.output); out.mkdir(parents=True,exist_ok=True)
sql='''BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SELECT json_build_object('Type','Metadata','Data',json_build_object('ExtractedAt',now(),'SchemaVersion',(SELECT max("MigrationId") FROM "__EFMigrationsHistory"),'Transaction',txid_current_snapshot()::text));
SELECT json_build_object('Type','Result','Data',row_to_json(r)) FROM results r ORDER BY date,division,"resultId";
SELECT json_build_object('Type','Fixture','Data',row_to_json(f)) FROM fixtures f ORDER BY date,division,"fixtureId";
COMMIT;'''
path=out/'snapshot.jsonl'
with path.open('wb') as f:
 subprocess.run(['docker','exec','-i','-e','PGOPTIONS=-c default_transaction_read_only=on',a.container,'psql','-XqAt','-v','ON_ERROR_STOP=1','-U','postgres','-d','power_lines_web'],input=sql.encode(),stdout=f,check=True)
rows=[]; fixtures=[]
with path.open() as f:
 for line in f:
  x=json.loads(line)
  if x['Type']=='Metadata': metadata=x['Data']
  elif x['Type']=='Result': rows.append(x['Data'])
  else: fixtures.append(x['Data'])
sha=hashlib.sha256(path.read_bytes()).hexdigest()
audit={'Sha256':sha,'CodeSha':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'Metadata':metadata,'Results':len(rows),'Fixtures':len(fixtures),'FirstResult':min(x['date'] for x in rows),'LastResult':max(x['date'] for x in rows),'Divisions':sorted(set(x['division'] for x in rows))}
from collections import Counter,defaultdict
cohorts=defaultdict(list)
for r in rows:
 d=datetime.date.fromisoformat(r['date'][:10]); season=d.year-(d.month<7); cohorts[(r['division'],season)].append(r)
def priced(r): return all(r[k]>1 for k in ('homeOddsAverage','drawOddsAverage','awayOddsAverage'))
audit['SeasonDivision']=[{'Division':k[0],'Season':k[1],'Count':len(v),'Priced':sum(priced(r) for r in v),'OverroundMin':min((sum(1/r[t] for t in ('homeOddsAverage','drawOddsAverage','awayOddsAverage')) for r in v if priced(r)),default=None),'OverroundMax':max((sum(1/r[t] for t in ('homeOddsAverage','drawOddsAverage','awayOddsAverage')) for r in v if priced(r)),default=None)} for k,v in sorted(cohorts.items())]
keys=Counter((r['division'],r['date'],r['homeTeam'],r['awayTeam']) for r in rows)
audit['DuplicateIdentities']=[list(k)+[v] for k,v in keys.items() if v>1]
audit['InvalidScores']=sum(r['fullTimeHomeGoals']<0 or r['fullTimeAwayGoals']<0 or r['fullTimeResult']!=('H' if r['fullTimeHomeGoals']>r['fullTimeAwayGoals'] else 'A' if r['fullTimeHomeGoals']<r['fullTimeAwayGoals'] else 'D') or r['homeTeam']==r['awayTeam'] for r in rows)
audit['QuotesUnqualified']=len(rows); audit['QuoteQualification']='No stored source mapping/hash/timing; qualify separately against frozen source files. Scores retained.'
audit['Aliases']='No stable source team IDs. Exact names retained; do not merge suspected aliases automatically.'
(out/'audit.json').write_text(json.dumps(audit,indent=2)+'\n')
print(json.dumps({k:v for k,v in audit.items() if k not in ('SeasonDivision',)},indent=2))
