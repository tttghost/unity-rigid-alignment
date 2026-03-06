# Rigid Alignment POC

Unity 기반 강체 정합(Rigid Alignment) POC 프로젝트.  
Real 모델(스캔/실물)과 Virtual 모델(CAD) 사이에 대응점(마커)을 찍어 최적 정합을 수행합니다.

## 주요 기능

- **SVD Kabsch 정합** — N점 기반 최적 회전/평행이동 계산
- **아웃라이어 자동 제거** — σ 기반 반복 필터링 (최대 3회)
- **IRLS 가중 정합** — Cauchy 가중함수, MAD 튜닝
- **마커 관리** — 8색 페어링, 프리뷰, 드래그 이동, 고정 크기 표시
- **실시간 시각화** — RMSE, 마커별 잔차 라벨 (3D TextMeshPro)
- **Clone 기반 정합** — 원본 고정, 복사본 이동 (반투명 셰이더)
- **iPad ARKit 지원** — AR Foundation + LiDAR 면인식 (테스트 단계)

## 환경

- **Unity** 6000.0 (Unity 6)
- **Render Pipeline**: URP 17.0.4
- **Input**: Old Input System
- **AR**: AR Foundation 6.0.6 + ARKit 6.0.6

## 프로젝트 구조

```
Assets/
├── Scripts/
│   ├── AlignmentController.cs    # 오케스트레이터
│   ├── RigidAlignment.cs         # SVD Kabsch 솔버
│   ├── MarkerManager.cs          # 마커 CRUD/페어링/프리뷰
│   ├── AlignmentVisualizer.cs    # Clone/RMSE/잔차 라벨
│   ├── Interfaces/
│   │   ├── IInputProvider.cs     # 입력 추상화
│   │   └── ISurfaceProvider.cs   # 표면 레이캐스트 추상화
│   ├── Implementations/
│   │   ├── ScreenInputProvider.cs    # 마우스/터치
│   │   └── SceneSurfaceProvider.cs   # Physics.Raycast
│   └── AR/
│       └── ARTapToAnchor.cs      # iPad ARKit 앵커 테스트
└── RigidAlignment/
    ├── Materials/
    ├── Models/
    ├── Prefabs/
    └── Scenes/
        ├── RigidAlignment-PC.unity       # PC/에디터용
        ├── RigidAlignment-iOS.unity      # iPad AR용
        └── RigidAlignment-iOS-Test.unity # iPad AR 테스트용
```

## 사용 방법

1. Unity 6에서 프로젝트 열기
2. `RigidAlignment-PC.unity` 씬 로드
3. Play → Real/Virtual 모델 표면 클릭으로 마커 배치
4. 3점 이상 배치 시 자동 정합 실행

## 조작

| 입력 | 동작 |
|------|------|
| 좌클릭 (표면) | 마커 배치 (Real→Virtual 교대) |
| 좌클릭 (마커) | 페어 마커 삭제 |
| 드래그 (마커) | 마커 이동 + 실시간 재정합 |
| ESC | 리셋 |
