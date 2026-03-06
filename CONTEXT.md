# Rigid Alignment POC — 프로젝트 컨텍스트

## 개요
- Unity 기반 Rigid Alignment(강체 정합) POC
- Real 모델(스캔/실물)과 Virtual 모델(CAD) 사이의 대응점(마커)을 찍어 정합
- POC 성과를 VirnectAR 패키지(AutoAlignment, InitialAlignment, ManualAlignment)로 통합 예정

## 워크스페이스 구조
- `virnectar-test-2026-02-24/` — POC 프로젝트 (Unity)
- `VirnectAR/` — 공용 패키지 (Module/ 하위에 빈 AutoAlignment, InitialAlignment, ManualAlignment 폴더 존재)

## 핵심 클래스 (Assets/Scripts/)
| 클래스 | 역할 |
|--------|------|
| AlignmentController | 오케스트레이터 (Input→Surface→Marker→Solve→Visualize) |
| RigidAlignment | SVD Kabsch 솔버 (아웃라이어 제거, IRLS 가중) |
| MarkerManager | 마커 CRUD, 8색 페어링, 프리뷰, 고정크기 |
| AlignmentVisualizer | Clone, RMSE, 잔차 라벨 풀링/빌보드 |
| IInputProvider / ScreenInputProvider | 입력 추상화 (마우스/터치) |
| ISurfaceProvider / SceneSurfaceProvider | 표면 레이캐스트 추상화 |
| ARTapToAnchor | iPad ARKit 테스트용 |

## 알고리즘 핵심
- Kabsch (SVD): U·V^T 회전행렬 (V·U^T 아님 — 이전 버그 수정)
- 아웃라이어: σ 기반 반복 필터링 (최대 3회)
- IRLS: Cauchy 가중함수, MAD 튜닝, 수렴 조건 (deltaPos<1e-7, deltaRot<1e-4)

## 완료 상태 (CHECKLIST.md 기준)
- ✅ 정합 알고리즘, 마커 관리, 드래그, 시각화, 리팩토링, 아키텍처 분리 — 전부 완료
- ✅ RigidAlignmentMono 삭제 완료 → AlignmentController로 대체

## 미완료 작업
- 정밀도: 마커 스냅(버텍스/엣지), ICP 미세 정합
- UX: Undo/Redo, 마커 넘버링, 결과 저장/로드(JSON)
- 확장: 다중 모델 정합, iPad ARKit 연동

## 씬 파일
- RigidAlignment-PC.unity — PC용 메인
- RigidAlignment-iOS.unity / RigidAlignment-iOS-Test.unity — iPad AR 테스트

## 이전 대화 히스토리 요약 (40턴, 2/25~3/5)
- 체크리스트(CHECKLIST.md) 생성 및 카테고리별 정리
- Kabsch 회전행렬 버그 수정 (스피어/메시콜라이더 정합 실패 → V·U^T를 U·V^T로 수정)
- 마커/라벨 고정 크기 + Inspector 배율 조절(SerializeField) 추가
- **패키지화 논의**: 사내 패키지(VirnectAR)가 아닌 개인 GitHub에 올리기로 결정 (사내 패키지엔 기존 수동정합이 있지만 사용 안 함)
- **리팩토링 순서 결정**: 백업 브랜치 → 리팩토링 브랜치 → 인터페이스 분리 → 구현 클래스 → 오케스트레이터 → 검증 → 머지
- **인터페이스 이름 논의**: EditorInputProvider → ScreenInputProvider (VR 컨트롤러도 고려), ARMeshSurface → 이름 재고
- **AR 구조 논의**: ARKit LiDAR 면인식 → AR Raycast Manager → ARRaycastHit (Physics.Raycast와 다름)
- **마지막 결정**: iPad에서 클릭→앵커 찍기 기본 코드(ARTapToAnchor) 선행 테스트

## 주의사항
- 코드에 TODO/FIXME 없음 (깨끗한 상태)
- Old Input System 사용 중
