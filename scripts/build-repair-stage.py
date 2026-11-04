#!/usr/bin/env python3
"""Generate guarded, idempotent SQL + inverse SQL from the reviewed stage. Never connects to DB."""
import argparse,json,pathlib,re,decimal
p=argparse.ArgumentParser();p.add_argument('--stage',required=True);p.add_argument('--output',required=True);a=p.parse_args();rows=json.loads(pathlib.Path(a.stage).read_text());out=pathlib.Path(a.output);out.mkdir(parents=True,exist_ok=True)
def num(x,offered=False):
 d=decimal.Decimal(str(x));assert d.is_finite() and (d>1 if offered else d>=0);return str(d)
def literal(x):return "'"+str(x).replace("'","''")+"'"
apply=['-- REVIEW ONLY. Requires web forecast-evidence migration and a verified pre-rollout backup.','BEGIN;','CREATE TEMP TABLE repaired_ids (id integer PRIMARY KEY);']
rollback=['-- REVIEW ONLY. Restore from verified backup for full provenance rollback. This inverse restores numeric prices only.','BEGIN;']
for row in rows:
 identity=row['Identity'];rid=int(row['ResultId']);before=list(map(num,row['Before']));after=[num(x,True) for x in row['After']];sha=row['SourceFileHash'];assert re.fullmatch('[a-f0-9]{64}',sha)
 cols=['homeOddsAverage','drawOddsAverage','awayOddsAverage'];guard=' AND '.join(f'"{c}"={v}' for c,v in zip(cols,before));assignment=', '.join(f'"{c}"={v}' for c,v in zip(cols,after))
 # Exact natural identity and source reconciled score must still match the frozen snapshot.
 same=f'"resultId"={rid} AND division={literal(identity[0])} AND date::date=DATE {literal(identity[1])} AND "homeTeam"={literal(identity[2])} AND "awayTeam"={literal(identity[3])}'
 score=row.get('ExpectedScore');scoreguard='' if score is None else f' AND "fullTimeHomeGoals"={int(score[0])} AND "fullTimeAwayGoals"={int(score[1])}'
 apply.append(f'WITH changed AS (UPDATE results SET {assignment}, "PriceKind"=\'AveragePreClosing\', "SourceUrl"={literal(row["SourceUrl"])}, "SourceFileHash"={literal(sha)}, "PriceCollection"=\'Source reconciled; individual observation time unknown\', "QuoteObservedAt"=NULL, "Updated"=clock_timestamp(), "Revision"="Revision"+1 WHERE {same}{scoreguard} AND {guard} RETURNING "resultId") INSERT INTO repaired_ids SELECT "resultId" FROM changed;')
 backguard=' AND '.join(f'"{c}"={v}' for c,v in zip(cols,after));backset=', '.join(f'"{c}"={v}' for c,v in zip(cols,before))
 rollback.append(f'UPDATE results SET {backset}, "PriceKind"=\'LegacyAverageUnknown\', "SourceFileHash"=NULL, "Updated"=clock_timestamp(), "Revision"="Revision"+1 WHERE {same} AND {backguard} AND "SourceFileHash"={literal(sha)};')
apply+=['SELECT count(*) AS changed FROM repaired_ids;','COMMIT;'];rollback+=['COMMIT;']
(out/'apply-reviewed-stage.sql').write_text('\n'.join(apply)+'\n');(out/'rollback-prices.sql').write_text('\n'.join(rollback)+'\n');print(f'Generated {len(rows)} guarded staged corrections. Not executed.')
