#!/usr/bin/env python3
"""Check the review image using an explicitly isolated HTTP endpoint."""
import argparse,urllib.request,json
p=argparse.ArgumentParser();p.add_argument('--url',required=True);a=p.parse_args()
for route,content in [('Fixtures','Win forecasts'),('Accuracy','Forecast evaluation')]:
 with urllib.request.urlopen(a.url+'/'+route,timeout=20) as r:
  body=r.read().decode();assert r.status==200;assert content in body;assert r.headers['X-Content-Type-Options']=='nosniff';assert r.headers['X-Frame-Options']=='DENY'
  print(route,r.status,len(body))
