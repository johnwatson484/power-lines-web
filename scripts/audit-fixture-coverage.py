#!/usr/bin/env python3
import argparse,json,datetime,collections,pathlib
p=argparse.ArgumentParser();p.add_argument('--snapshot',required=True);p.add_argument('--output',required=True);a=p.parse_args()
results=collections.defaultdict(list);fixtures=[]
for line in open(a.snapshot):
 x=json.loads(line)
 if x['Type']=='Result': results[x['Data']['division']].append(x['Data'])
 elif x['Type']=='Fixture':fixtures.append(x['Data'])
counts=collections.Counter();details=[]
for f in fixtures:
 day=datetime.datetime.fromisoformat(f['date']).date();start=day.replace(year=day.year-6)
 train=[r for r in results[f['division']] if start<=datetime.date.fromisoformat(r['date'][:10])<day]
 hv=sum(r['homeTeam']==f['homeTeam'] for r in train);av=sum(r['awayTeam']==f['awayTeam'] for r in train)
 ht=sum(f['homeTeam'] in (r['homeTeam'],r['awayTeam']) for r in train);at=sum(f['awayTeam'] in (r['homeTeam'],r['awayTeam']) for r in train)
 counts['Fixtures']+=1;counts['LegacyVenue10']+=hv>=10 and av>=10;counts['Total10']+=ht>=10 and at>=10;counts['Total5']+=ht>=5 and at>=5
 counts['UnknownTeam']+=ht==0 or at==0;counts['KnownThinHistory']+=ht>0 and at>0 and (hv<10 or av<10)
 details.append({'Id':f['fixtureId'],'Division':f['division'],'HomeVenue':hv,'AwayVenue':av,'HomeTotal':ht,'AwayTotal':at})
pathlib.Path(a.output).write_text(json.dumps({'Counts':dict(counts),'Fixtures':details},indent=2)+'\n');print(dict(counts))
