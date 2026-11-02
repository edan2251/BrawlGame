namespace Knockbox.Simulation
{
    /// <summary>
    /// CR-004 실험 경기의 수치 덮어쓰기(S-160·S-161). 튜닝 에셋 값 위에 얹는다. 실행기와 테스트가 같은 값을 쓴다.
    /// </summary>
    public static class BrawlParams
    {
        /// <summary>KO 60틱 + 카운트다운 30틱 ≈ KO 뒤 1.5초에 다음 라운드, 3선승(9절 기본값).</summary>
        public static MatchParams Match(MatchParams match)
        {
            match.CountdownTicks = 30;
            match.KoTicks = 60;
            match.RoundsToWin = 3;
            return match;
        }

        /// <summary>
        /// 단계 A 피드백: 돌진이 느리고 길게 일직선으로 간다 → 더 빠르게 나가서 강하게 감속하고 일찍 끝난다(약 2.5~6칸).
        /// 지형에 부딪히면 반사 대신 멈추고 작은 반동만(고무벽 느낌 제거, 5.1 "벽에 닿음: 종료").
        /// </summary>
        /// <param name="surfaceMode">표면 이동(7절): 0 끔, 1 D, 2 E.</param>
        public static CombatParams Combat(CombatParams combat, int surfaceMode = 0)
        {
            combat.Charge.FullChargeTicks = 24;
            combat.Charge.LungeMinSpeed = 24f;
            combat.Charge.LungeMaxSpeed = 38f;
            combat.Charge.LungeDrag = 90f;
            combat.Charge.LungeEndSpeed = 12f;
            combat.Charge.LungeMaxTicks = 14;
            combat.Charge.LungeMaxBounces = 0;
            combat.Charge.LungeStopRecoil = 4f;
            combat.Charge.LungeStopKeep = 0.25f;
            combat.Body = BodyParams.Default;
            // 단계 C(5.2): 마우스로 겨누고 클릭하기에 20틱은 짧다 → 60틱. 피드백 뒤로는 자동 던짐 대신 그때 놓아준다.
            combat.Grab.MaxHoldTicks = 60;
            // 단계 C 피드백: 잡기 판정이 패링처럼 빡빡하다 → 선딜 5 → 3, 활성 3 → 9, 상자 0.7×0.9 → 1.1×1.3(중심 0.65 → 0.75, 커서 방향).
            combat.Grab.StartupTicks = 3;
            combat.Grab.ActiveTicks = 9;
            combat.Grab.BoxOffset = 0.75f;
            combat.Grab.BoxWidth = 1.1f;
            combat.Grab.BoxHeight = 1.3f;
            // 던지기는 일직선으로 거의 반대편까지: 속도 28 → 48, 던져진 상태 24 → 30틱(그동안 중력 0.2배).
            combat.Grab.ThrowSpeed = 48f;
            combat.Grab.ThrownTicks = 30;
            combat.Body.SurfaceMode = surfaceMode;
            return combat;
        }
    }
}
