using System.Collections;
using Knockbox.Game;
using Knockbox.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Knockbox.Tests.PlayMode
{
    /// <summary>
    /// CR-004 단계 S(8절): 외형 샘플이 콘솔 오류 없이 모든 포즈를 그리고, 판정 오버레이가 켜지며, 몸통 폭이 판정 폭의 1~1.25배이고,
    /// 네 방향 주먹을 끝까지 뻗은 순간 주먹 끝이 주먹 판정 원 안에 있는지 숫자로 확인한다(화면 촬영 없음).
    /// </summary>
    public class CharacterSampleTests
    {
        GameObject cameraObject;
        CharacterSampleScene sample;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            sample = new GameObject("Character Sample").AddComponent<CharacterSampleScene>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(sample.gameObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllPoses_RenderWithoutErrors_OverlayShown_FistInsideCircle()
        {
            Assert.IsTrue(sample.OverlayShown, "판정 오버레이가 기본으로 켜짐");
            Assert.That(sample.TorsoRatio, Is.InRange(1f, 1.25f), "보이는 몸통 폭 = 판정 폭의 1~1.25배");
            foreach (DollPoseKind kind in System.Enum.GetValues(typeof(DollPoseKind)))
            {
                sample.SetPose(kind);
                float until = Time.time + DollPoses.PunchCycle * 1.2f;
                while (Time.time < until)
                    yield return null;
                bool punch = kind == DollPoseKind.PunchForward || kind == DollPoseKind.PunchUp || kind == DollPoseKind.PunchDown || kind == DollPoseKind.PunchDiagonal;
                if (punch)
                {
                    Assert.IsTrue(sample.FistChecked, $"{kind}: 주먹을 끝까지 뻗은 순간을 확인함");
                    Assert.IsTrue(sample.FistOk, $"{kind}: 주먹 끝이 판정 원 안");
                }
            }
            sample.Overlay = false;
            yield return null;
            Assert.IsFalse(sample.OverlayShown, "끄면 숨김");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
