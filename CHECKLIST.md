# Rigid Alignment POC 체크리스트

## 완료

### 핵심 정합 알고리즘
- [x] 초기 Rigid Alignment POC 구현 (3점 기반)
- [x] Kabsch 알고리즘 N점 최적 정합 (SVD 기반)
- [x] Clone 기반 정합 (원본 고정, 복사본 이동)
- [x] 아웃라이어 자동 제거 (σ 기반 반복 필터링)
- [x] 가중 Kabsch (IRLS) 정합 (Cauchy 가중함수, MAD 튜닝)
- [x] Kabsch 회전행렬 공식 수정 (V·U^T → U·V^T, 비축정합 버그 해결)

### 마커 관리
- [x] 마커 프리뷰 + 클릭 삭제 기능
- [x] 마커를 독립 오브젝트로 변경 (스케일 왜곡 방지)
- [x] 마커 컬러 페어링 (8색 팔레트)
- [x] 페어 마커 동시 삭제
- [x] 프리뷰 마커에 페어 색상 반영 (호버 시 다음 마커 색상)
- [x] 마커 계층 정리 (RealMarkers, VirtualMarkers 부모 오브젝트)
- [x] 마커 카메라 거리 무관 고정 크기 (프리뷰 포함)
- [x] 마커/라벨 표시 배율 Inspector 조절 (SerializeField)

### 드래그
- [x] 마커 드래그 이동 기능
- [x] 마커 드래그 실시간 정합 + 표면 이탈 즉시 원복

### 시각화 (잔차/RMSE)
- [x] RMSE + 마커별 잔차 시각화 (TextMeshPro 3D)
- [x] 잔차 라벨 Overlay 머티리얼 (오브젝트에 가려지지 않음)
- [x] 잔차 라벨 풀링 (GC 방지)
- [x] virtual 쪽 잔차 라벨 추가 (양쪽 동시 표시)
- [x] 잔차 라벨 카메라 거리 무관 고정 크기
- [x] 고정 크기 깊이 계산을 카메라 forward 축 depth 기반으로 변경
- [x] Clone 머티리얼 지원 (반투명 셰이더)

### 리팩토링 / 정리
- [x] real/virtual 용어 정리 및 변수명 리팩토링
- [x] 마커 프리팹/머티리얼 리네임 정리
- [x] 씬 Hierarchy 정리 (Environment, Real, Virtual, UI, System)
- [x] Assets 폴더 구조 재배치 (Materials, Prefabs, Scenes)

## 예정

### 정밀도 개선
- [ ] 마커 스냅 (메시 버텍스/엣지에 스냅)
- [ ] ICP 기반 자동 미세 정합 (마커 정합 후 메시 간 보정)

### UX
- [ ] Undo/Redo (마커 추가/삭제/드래그 이력 관리)
- [ ] 마커 넘버링 (마커 위에 페어 번호 표시)
- [ ] 정합 결과 저장/로드 (JSON 직렬화)

### 확장
- [ ] 다중 모델 정합 (여러 가상 모델 독립 정합)
- [ ] iPad ARKit 연동 (AR Foundation + 실환경 스캔 메시)

### 코드 품질 / 아키텍처
- [x] 인터페이스 추상화 (IInputProvider, ISurfaceProvider + SurfaceHitResult)
- [x] ScreenInputProvider 구현 (마우스/터치 공용, Old Input System)
- [x] SceneSurfaceProvider 구현 (Physics.Raycast + Transform 비교)
- [x] MarkerManager 분리 (CRUD, 컬러 페어링, 프리뷰, 고정 크기 스케일링)
- [x] AlignmentVisualizer 분리 (Clone, RMSE, 잔차 라벨 풀링/빌보드)
- [x] AlignmentController 오케스트레이터 작성 (RigidAlignmentMono 대체)
- [x] RigidAlignment.Solve 파라미터 IReadOnlyList<Vector3>로 확장
- [x] 씬 연결: RigidAlignmentMono → AlignmentController 교체 후 동작 검증
- [x] RigidAlignmentMono 삭제 (AlignmentController 검증 완료)
