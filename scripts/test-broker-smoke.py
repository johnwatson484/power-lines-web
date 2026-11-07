#!/usr/bin/env python3
"""Exercise fanout transport against an explicitly isolated broker and PostgreSQL database."""
import argparse,base64,datetime,json,pathlib,subprocess,time,urllib.request
p=argparse.ArgumentParser();p.add_argument('--broker-url',required=True);p.add_argument('--url',required=True);p.add_argument('--database-container',required=True);p.add_argument('--database',required=True);p.add_argument('--output',required=True);a=p.parse_args()
assert a.database.startswith('power_lines_test_') and a.database_container.startswith('power-lines-win-odds-test')
auth='Basic '+base64.b64encode(b'integration:integration').decode()
def publish(exchange,payload):
 data={'properties':{'delivery_mode':2},'routing_key':'','payload':json.dumps(payload),'payload_encoding':'string'}
 request=urllib.request.Request(a.broker_url+'/api/exchanges/%2F/'+exchange+'/publish',data=json.dumps(data).encode(),headers={'Authorization':auth,'Content-Type':'application/json'})
 with urllib.request.urlopen(request,timeout=10) as response:assert json.load(response)['routed']
def query(sql):return subprocess.check_output(['docker','exec',a.database_container,'psql','-XqAt','-U','postgres','-d',a.database,'-c',sql],text=True).strip()
def until(sql,expected):
 for attempt in range(40):
  value=query(sql)
  if value==expected:return
  time.sleep(.2)
 raise AssertionError((sql,value,expected))
fixture={'Division':'E0','Date':'2026-11-07T15:00:00','HomeTeam':'TransportHome','AwayTeam':'TransportAway','HomeOddsAverage':2.1,'DrawOddsAverage':3.4,'AwayOddsAverage':3.5,'SourceUrl':'https://example.test/fixtures.csv','SourceFileHash':'transport-1','SourceMatchId':'transport-fixture','PriceKind':'AveragePreClosing','SourcePrices':[{'Columns':'AvgC','Kind':'AverageClosing','Home':2.2,'Draw':3.5,'Away':3.6}]}
publish('fixtures',fixture)
until('SELECT count(*) FROM fixtures WHERE "SourceMatchId"=\'transport-fixture\';','1')
created=query('SELECT "Created" FROM fixtures WHERE "SourceMatchId"=\'transport-fixture\';')
publish('fixtures',fixture);fixture['HomeOddsAverage']=2.4;fixture['SourceFileHash']='transport-2';publish('fixtures',fixture)
until('SELECT "Revision" FROM fixtures WHERE "SourceMatchId"=\'transport-fixture\';','2')
assert query('SELECT "Created" FROM fixtures WHERE "SourceMatchId"=\'transport-fixture\';')==created
result={'Division':'E0','Date':'2026-11-01T00:00:00','HomeTeam':'TransportHome','AwayTeam':'TransportAway','FullTimeHomeGoals':2,'FullTimeAwayGoals':1,'FullTimeResult':'H','SourceMatchId':'transport-result'}
publish('results',result);until('SELECT count(*) FROM results WHERE "SourceMatchId"=\'transport-result\';','1')
created_result=query('SELECT created FROM results WHERE "SourceMatchId"=\'transport-result\';');result.update(FullTimeHomeGoals=1,FullTimeAwayGoals=2,FullTimeResult='A');publish('results',result)
until('SELECT "Revision" FROM results WHERE "SourceMatchId"=\'transport-result\';','2');assert query('SELECT created FROM results WHERE "SourceMatchId"=\'transport-result\';')==created_result
status={'SourceUrl':'https://example.test/fixtures.csv','Division':'E0','SourceSeason':'2627','SourceFileHash':'transport-2','RowCount':1,'CompletedAt':'2026-11-04T20:00:00Z','Status':'Completed'}
publish('imports',status);until('SELECT count(*) FROM import_statuses;','1')
for route in ('Fixtures','Accuracy'):
 with urllib.request.urlopen(a.url+'/'+route,timeout=10) as response:
  body=response.read().decode();assert response.status==200
  assert ('TransportHome' in body if route=='Fixtures' else 'Forecast evaluation' in body)
report={'Network':'isolated disposable network/broker/PostgreSQL','FixtureRevisionAfterIdenticalAndChangedQuote':2,'ResultRevisionAfterCorrection':2,'CreatedPreserved':True,'CompletedImportEvents':1,'HttpFixtureAndAccuracy':200,'ActualAvailableOffers':0}
pathlib.Path(a.output).write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
