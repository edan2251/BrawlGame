# CR-004 단계 S 기록: 캐릭터 외형 (보류, 나중에 이어서)

> 2026-11-02 사용자 지시로 **게임 화면의 캐릭터 외형은 처음의 2D 사각형 캐릭터(FighterView)로 되돌렸다.** 아래 작업은 지우지 않고 보존했으며, 나중에 이 문서부터 읽고 이어서 만든다.
> 관련 결정: S-170(S1 샘플), S-171(R3·R4), S-172(R3 피드백), D-071·S-173(3D 시험 외형), D-072(되돌림).

## 1. 목표와 사용자 기준

- 요청서 8절: 휴먼 폴 플랫풍의 통통한 사람형 인형. 기존 IK(`TwoBoneIK`) 재사용, 판정과 외형이 어긋나지 않게(보이는 몸통 폭 = 판정 폭의 1~1.25배, 주먹 끝이 주먹 판정 원 안).
- 사용자 레퍼런스(대화에 준 이미지): Gang Beasts 캐릭터의 **느낌**. 머리·몸·다리가 이어진 말랑한 덩어리, 무릎 없는 짧은 다리, 팔꿈치가 드러나지 않는 굵은 튜브 팔과 벙어리장갑 손, 단순한 흰 점 눈. 옷·후드·얼굴 같은 고유 디자인은 따라 하지 않는다.
- R3에 대한 사용자 피드백(2026-11-02, 반영함): 배처럼 보임 → 머리는 조금 납작한 둥근 사각형, 어깨는 사람 위치(앞에 달리지 않게), 팔에 따로 외곽선 없이 굵고 굴곡지게, 뒤쪽·회전은 명암으로, 다리 실루엣이 보이게(보자기 느낌 없이), 눈은 젤리처럼 살짝 출렁이게.

## 2. 만든 것 (모두 저장소에 보존)

| 이름 | 방식 | 파일 | 상태 |
|---|---|---|---|
| 공통 뼈대·포즈 | 원점 = 판정 상자 중심(몸 0.72), 포즈 10개(가만히, 달리기, 주먹 앞·위·아래·대각, 천장, 박치기, 잡힘, 던져짐) | `Scripts/Presentation/Sample/DollSkeleton.cs` | 완료 |
| R1 부위 메시 + 관절 구 | 늘린 원 부위, 부위마다 잉크 외곽선 | `Sample/DollPartsView.cs` | 완료(사용자 의견 없음) |
| R2 IK 튜브 + 스프링 몸통 | 매 프레임 튜브 메시, 말랑한 24점 몸통 | `Sample/DollTubeView.cs` | 완료(사용자 의견 없음) |
| **R3 SDF** (사용자 피드백 반영 중) | 셰이더 하나로 부위를 거리장으로 합침, 실루엣 외곽선 하나 + 명암, 스프링 눈 | `Art/Shaders/SdfDoll.shader`, `Sample/DollSdfView.cs` | 피드백 1회 반영, **사용자 확인 전에 보류** |
| R4 2D 스키닝 | 코드만으로 이미지·뼈 3개·메시·가중치·SpriteSkin 연결(550 ms, 사람 손 0단계) | `Editor/R4SkinningProbe.cs`, `Art/Samples/R4Limb.png`, `Game/Debug/R4LimbWiggle.cs` | 자동화 가능 확인만 |
| 샘플 씬 | R1·R2·R3 나란히, 숫자키 1~0 포즈, O 판정 오버레이(몸통 폭 비율·주먹 끝 확인) | `Scenes/CharacterSample.unity`(메뉴 `Knockbox/Setup/Create Character Sample Scene`), `Game/Debug/CharacterSampleScene.cs` | 열어서 바로 볼 수 있음 |
| 3D 시험 외형 | Gang Beast Rigged(GLB → Blender 5.2로 FBX), Generic 리그, 애니메이션 없이 절차적 뼈 돌리기(이동·몸 방향·커서 방향 주먹) | 코드: `Presentation/Fighter/FighterModelRig.cs`, `FighterModelSettings.cs`, `Art/Shaders/ModelUnlit.shader`, `Editor/TestFighterModelBuilder.cs`, `MatchPresenter.CreateTestModel`. 모델·프리팹·설정·출처: `Assets/_Project/LocalOnly/`(Git·빌드 제외) | 첫 샘플까지, **꺼 둠** |

테스트(PlayMode): `CharacterSampleTests`(세 후보가 몸 색으로 그려짐, 오버레이, 네 방향 주먹이 판정 원 안 — R3 포함), `R4SkinningTests`(뼈를 돌리면 변형), `TestFighterModelTests`(로컬 모델이 켜져 있을 때만, 꺼져 있으면 건너뜀).

## 3. 지금 상태

- 게임(Main 씬)의 두 캐릭터는 처음의 2D 사각형 캐릭터(`FighterView`)다. 경기 규칙·판정·이동(벽 기어오르기 포함)은 그대로.
- 3D 시험 외형은 로컬 설정 `Assets/_Project/LocalOnly/FighterModelSettings.asset`의 `use`를 꺼서 쓰지 않는다. 코드와 로컬 파일은 남아 있다.
- 샘플 씬(`CharacterSample.unity`)은 게임과 따로라 그대로 볼 수 있다.

## 4. 이어서 할 때

1. 2D(R3) 쪽: `CharacterSample.unity`에서 R3를 보고 사용자 피드백을 받는다. 정해지면 R3를 실제 경기 외형(`FighterView` 대신 또는 함께)으로 연결하는 작업을 따로 계획한다(외형은 시뮬레이션 상태만 읽는다).
2. 3D 쪽: `FighterModelSettings.asset`의 `use`를 켜면 Main 씬 P1이 3D 모델로 보인다. 로컬 모델이 없는 PC에서는 모델 파일을 `LocalOnly/FighterModels/GangBeastTest/`에 두고 메뉴 `Knockbox/Setup/Build Test Fighter Model (Local)`을 실행한다. 다음 순서는 잡기·던지기·벽·천장 동작(사용자 확인 뒤).
3. 남은 확인: R3의 사용자 판단, 3D 모델 크기(보이는 키 1.15)·화면 쪽 기울기 25°·달리기 흔들림·주먹 느낌, 3D 셰이더 깊이와 2D 효과의 겹침 순서.

## 5. 권리와 공개 주의 (3D 시험 모델)

- 출처: https://sketchfab.com/3d-models/gang-beast-rigged-6ab8aa50675048a4bfbbf3b849951751, 업로더 2031gabrielnaas, 표시 라이선스 Creative Commons Attribution 4.0, 게시일 2025-11-28.
- 원작 게임 관련 권리와 배포 허용 여부는 검증되지 않았다. 내부 시험용으로만 쓰고 공개 빌드·저장소·외부에 올리지 않는다(`LocalOnly`는 .gitignore, 에디터에서만 읽음). 최종 배포 에셋이 아니며 교체할 수 있다.
