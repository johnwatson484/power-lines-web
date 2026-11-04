#!/usr/bin/env python3
"""Freeze source CSVs and qualify prices by exact identity + score + average triplet.
Writes staging/report only. Never imports, publishes messages or updates the DB.
"""
import argparse,csv,datetime,hashlib,io,json,pathlib,urllib.request,concurrent.futures
p=argparse.ArgumentParser();p.add_argument('--snapshot',required=True);p.add_argument('--output',required=True);a=p.parse_args();out=pathlib.Path(a.output);out.mkdir(parents=True,exist_ok=True);cache=out/'sources';cache.mkdir(exist_ok=True)
rows=[x['Data'] for line in pathlib.Path(a.snapshot).open() if (x:=json.loads(line))['Type']=='Result']
divs=sorted(set(r['division'] for r in rows)); jobs=[(season,div) for season in ['1718','1819','2122','2223','2324','2425','2526','2627'] for div in divs]
def fetch(job):
 season,div=job;url=f'https://www.football-data.co.uk/mmz4281/{season}/{div}.csv';file=cache/f'{season}-{div}.csv'
 try:
  if not file.exists():
   with urllib.request.urlopen(url,timeout=45) as response: file.write_bytes(response.read())
  data=file.read_bytes();text=data.decode('utf-8-sig',errors='replace'); parsed=list(csv.DictReader(io.StringIO(text)))
  if not parsed or 'Date' not in parsed[0]: raise ValueError('Not a results CSV')
  return (job,url,hashlib.sha256(data).hexdigest(),parsed,None)
 except Exception as e:return(job,url,None,[],str(e))
results={};files=[]
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:
 for job,url,sha,parsed,error in ex.map(fetch,jobs):
  files.append({'Season':job[0],'Division':job[1],'Url':url,'Sha256':sha,'Rows':len(parsed),'HasTime':bool(parsed and 'Time' in parsed[0]),'Error':error})
  for r in parsed:
   try:
    date=None
    for fmt in ('%d/%m/%Y','%d/%m/%y','%Y-%m-%d'):
     try:date=datetime.datetime.strptime(r['Date'],fmt).date().isoformat();break
     except ValueError:pass
    key=(r['Div'],date,r['HomeTeam'],r['AwayTeam'])
    avg=[float(r.get('Avg'+k) or r.get('BbAv'+k) or 0) for k in 'HDA'];mx=[float(r.get('Max'+k) or r.get('BbMx'+k) or 0) for k in 'HDA']
    if any(x<=1 for x in avg):continue
    results[key]=(avg,mx,r,url,sha)
   except (KeyError,ValueError,TypeError):pass
report={};qualified=[];stage=[]
for row in rows:
 key=(row['division'],row['date'][:10],row['homeTeam'],row['awayTeam']);source=results.get(key)
 if source is None:continue
 avg,mx,r,url,sha=source;cohort=url.rsplit('/',2)[1]+'/'+row['division'];counts=report.setdefault(cohort,{'Matched':0,'AverageEqual':0,'MaximumEqual':0,'ScoreMismatch':0,'PriceCorrection':0});counts['Matched']+=1
 if (int(r.get('FTHG') or -1),int(r.get('FTAG') or -1))!=(row['fullTimeHomeGoals'],row['fullTimeAwayGoals']):counts['ScoreMismatch']+=1;continue
 old=[row[k] for k in ('homeOddsAverage','drawOddsAverage','awayOddsAverage')];equal=lambda x,y:all(abs(a-b)<1e-8 for a,b in zip(x,y))
 counts['AverageEqual']+=equal(old,avg);counts['MaximumEqual']+=equal(old,mx)
 if not equal(old,avg):counts['PriceCorrection']+=1
 # An offline corrected-source comparison is explicit; never claim these were executable offers.
 qualified.append({'Id':row['resultId'],'Home':avg[0],'Draw':avg[1],'Away':avg[2],'SourceUrl':url,'SourceFileHash':sha,'PriceKind':'AveragePreClosing','ObservedAt':None,'StoredAverageMatched':equal(old,avg)})
 if not equal(old,avg):stage.append({'ResultId':row['resultId'],'Identity':key,'ExpectedScore':[row['fullTimeHomeGoals'],row['fullTimeAwayGoals']],'Before':old,'After':avg,'SourceUrl':url,'SourceFileHash':sha,'ExpectedBeforeSha256':hashlib.sha256(json.dumps(old,separators=(',',':')).encode()).hexdigest()})
(out/'qualified-quotes.json').write_text(json.dumps(sorted(qualified,key=lambda x:x['Id']),indent=2)+'\n')
(out/'staged-price-corrections.json').write_text(json.dumps(stage,indent=2)+'\n')
(out/'source-reconciliation.json').write_text(json.dumps({'Files':files,'Cohorts':report,'QualifiedQuotes':len(qualified),'StagedCorrections':len(stage),'Applied':False,'Convention':'Source averages reconciled by score/identity; timing unknown, retrospective comparisons only.'},indent=2)+'\n')
print(json.dumps({'QualifiedQuotes':len(qualified),'StagedCorrections':len(stage),'E0_1819':report.get('1819/E0'),'FailedSources':sum(bool(x['Error']) for x in files)},indent=2))
