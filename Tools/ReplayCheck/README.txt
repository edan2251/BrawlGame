Knockbox 다른 PC 결정론 확인 (CR-004 온라인 1단계)

무엇을 하나
  같은 빌드 + 같은 입력 기록(brawl_sample.kbrp)을 재생해서, 매 틱(1/60초) 경기 상태의 체크섬을
  만든 PC의 결과(reference\trace.txt)와 비교한다. 네트워크·설치·가입은 필요 없다.
  기록: 1361틱(약 23초), 주먹·박치기·잡기·던지기·벽꽝·벽 기어오르기·KO·라운드 전환 포함.

실행 방법 (다른 PC)
  1. 이 폴더 전체(압축 파일)를 받아 아무 곳에나 압축을 푼다. 경로에 한글이 있어도 된다.
  2. ReplayCheck.bat 을 더블클릭한다.
     - Windows SmartScreen 경고가 뜨면 "추가 정보" → "실행"(서명하지 않은 개발 빌드라서).
     - 게임 창은 뜨지 않는다. 수 초 뒤 검은 창에 결과가 두 번(1회차·2회차) 나온다.
  3. 결과 줄이 "결과: PASS" 이면 그 PC에서 모든 틱이 일치한 것이다.
     "결과: FAIL" 이면 처음 다른 틱과 그 앞 틱의 상태 덤프(dump_t틱.txt)가 results 폴더에 생긴다.
     "결과: ERROR" 면 실행 자체가 실패한 것이다(파일 누락 등).
  4. 결과와 상관없이 results 폴더를 통째로 압축해서 보낸다(메신저·USB·클라우드 아무거나).
     results 안: run1·run2 각각 trace.txt(틱별 체크섬), result.txt(요약), player.log, (불일치 시) dump_t*.txt.
     result.txt 머리말에 운영체제와 CPU 이름이 적힌다. 컴퓨터 이름·사용자 이름은 적지 않는다
     (player.log 에는 폴더 경로가 들어갈 수 있다).

만든 PC에서 결과를 받은 뒤
  - FAIL이면 상대의 처음 다른 틱 T에 대해 이 PC에서 같은 틱 덤프를 뽑는다:
      Knockbox.exe -batchmode -nographics -replaycheck brawl_sample.kbrp -dumpticks T-1,T -out mine
    두 PC의 dump_tT.txt 를 줄 단위로 비교하면 처음 달라진 필드(이름 = 값 [비트])가 보인다.
  - 체크섬은 감지용이다. 불일치를 자동으로 고치지 않는다.

명령줄 인수 (개발 빌드 전용)
  -replaycheck 기록.kbrp   재생할 기록
  -expect trace.txt        비교할 기준 추적
  -dumpticks 10,11         이 틱들의 전체 상태를 dump_t틱.txt 로
  -out 폴더                결과 폴더(기본: 실행 파일 옆 results)
  종료 코드: 0 일치, 1 불일치, 2 실행 오류
