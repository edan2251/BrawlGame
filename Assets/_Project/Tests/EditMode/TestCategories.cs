namespace Knockbox.Tests.EditMode
{
    /// <summary>
    /// 테스트 묶음(CP17-2). 개발 중에는 빠른 묶음(Slow 제외: 카테고리 필터 "!Slow")만 돌리고, CP를 마칠 때 전체를 돌린다.
    /// </summary>
    public static class TestCategories
    {
        /// <summary>한 건에 수십 초 이상 걸리는 탐색·측정.</summary>
        public const string Slow = "Slow";
    }
}
