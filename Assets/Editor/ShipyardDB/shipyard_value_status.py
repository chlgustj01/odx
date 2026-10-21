"""Additive value provenance policy. Does not rewrite workbook confidence A/B/C."""
VALUE_STATUS_CODEBOOK={"schemaVersion":1,"policy":"value_status is independent of legacy confidence/source grades; preserve unfamiliar codes verbatim","codes":[{"code":"OFFICIAL","meaning":"공식/1차 출처에서 직접 확인된 항목"},{"code":"SECONDARY","meaning":"2차 자료에서 확인되었으나 공식/1차 출처에서는 직접 확인되지 않은 값"},{"code":"ESTIMATED","meaning":"모델링 목적의 추정값"},{"code":"DEMO","meaning":"시연용 가상값"},{"code":"UNKNOWN","meaning":"확인되지 않은 값"}]}
DIMENSION_NOTE="2차 자료 기반 제원 / 공식 시설 페이지에서 치수 직접 확인되지 않음"
def apply_value_status(facilities,floating):
    f=next(x for x in facilities if x['facilityId']=='F14')
    f.update(function_value_status='OFFICIAL',function_capabilities=['Suezmax-class launching capability','78,000-ton-class LNG carrier re-floating / repair capability'],function_source='\n'.join(x for x in f['source'].splitlines() if x.startswith('한화오션 공식 홈페이지') or x.startswith('Hanwha Ocean official site')),dimension_value_status='SECONDARY',dimension_status_note=DIMENSION_NOTE)
    for d in floating:
        d.update(value_status='SECONDARY',value_status_note=DIMENSION_NOTE,official_dimension_confirmed=False)

    # User-confirmed value-level evidence; never promotes the rest of a facility.
    source="https://www.hanwhaocean.com/en/whoweare/ig/facilities/"
    date="2026-10-20"
    by_id={x['facilityId']:x for x in facilities}
    def evidence(key,label,value,unit,status,source_text=source,verified=None,note=None,status_detail=None):
        return dict(key=key,label=label,value=value,unit=unit,value_status=status,source=source_text,verified_date=verified,note=note,status_detail=status_detail)
    for fid in ['F01','F02']:
        r=by_id[fid]
        value=' × '.join(format(r[k],'g') for k in ['lengthM','widthM','depthM'])
        r.update(dimension_value_status='OFFICIAL',value_evidence=[evidence('dock_dimension','DOCK DIMENSION',value,'m','OFFICIAL',verified=date)])
    by_id['F09']['value_evidence']=[evidence('total_quay_length','TOTAL QUAY LENGTH','3600','m','OFFICIAL',verified=date),evidence('water_depth','WATER DEPTH','11–14','m','OFFICIAL',verified=date),evidence('segment_breakdown','3D SEGMENT BREAKDOWN','600 / 1700 / 1300','m','DEMO',source_text='Scene reconstruction; not official quay geometry',note='3D 재구성용 분할이며, GIS 단계에서 실제 안벽선 geometry로 교체 예정')]
    by_id['F15']['value_evidence']=[evidence('area','AREA','45,000','㎡','OFFICIAL',verified=date),evidence('footprint_shape','FOOTPRINT SHAPE','Square / RECONSTRUCTED',None,'ESTIMATED',source_text='Scene footprint reconstruction',note='면적만 공식 확인값이며 정사각형 종횡비는 추정이다.')]
    note="공식 자료는 '야드 최대 단일 공장 210×54m'만 기재하며, 해당 공장이 F19라고 특정되지 않음"
    by_id['F19'].update(dimension_value_status='ESTIMATED',dimension_status_note=note,value_evidence=[evidence('dimension','DIMENSION','210 × 54','m','ESTIMATED',note=note,status_detail='Facility not specifically identified')])
