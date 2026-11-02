플러디드 캠페인 맵 (L4D2 / World War Z / Back 4 Blood 스타일, 약 2 km)

만드는 법
  1. 이 zip의 Assets 폴더를 프로젝트 폴더에 덮어쓴다.
  2. Unity에서 컴파일이 끝나면 메뉴 Tools > L4D 맵 > 플러디드 캠페인 맵 만들기 (기존 맵 삭제)
  3. "만들기 + 오클루전 굽기"(추천) 또는 "만들기만"을 고른다. 몇 분 걸린다.
  4. 끝나면 보고서(Assets/L4DCampaign/CampaignReport.txt)가 열린다. ▶ Play.

하는 일
  - L4D.unity를 Assets/01.Scenes/Backup/L4D_before_campaign.unity 하나로 백업
  - 씬의 예전 맵을 지우고 (Player, 카메라, UI, GameManager, AIDirector, 스포너, Global Volume은 유지) 새 맵 생성
  - 조립 건물은 머티리얼별로 합친 메시 + LODGroup + 합친 콜라이더 1개로 만들어 그리기 횟수를 줄임
  - 지형(1024 m), 물, 나무(URP Lit), 담장/폐차/선착장, 보이지 않는 경계벽, 길 안내 조명탄, 드럼통, 조명
  - CityMapInfo(경로 179개 지점, 스폰 257곳, 출발/중간 안전가옥, 피날레 정원, 무전기, 탈출 선착장) 연결
  - NavMesh 굽기 → 경로가 끊긴 곳 / 경계 밖 누수 / 스폰 연결 / 분홍 머티리얼 검사
  - 새 맵이 저장된 뒤 예전 맵 파일 삭제 (Backup_Map, Backup_15_16, L4D_LinearCampaignGenerated,
    L4DMap/Kenney와 K_*.mat, 예전 맵 도구 스크립트, 예전 Flooded 맵 데이터/NavMesh, 예전 백업 씬)

다른 메뉴
  - 캠페인 맵 NavMesh 다시 굽기 + 검사
  - 캠페인 맵 검사 (경로 길이·막힘·누수)
  - 캠페인 맵 오클루전 컬링 굽기

구역 순서
  출발 오두막 → 늪지 둑길 → 섬 → 물가 마을 → 농장 → 온실 통과 → 공장 지대 → 공장 마당(전투) → 뒷골목
  → 강변 도로 → 철교 → 강변 → 중간 안전가옥(오두막) → 읍내 → 교회 광장(전투) → 뒷길 → 읍내 서쪽
  → 저택 통과 → 정원(피날레: 무전기 [E], 버티기) → 선착장 끝 보트(탈출)
