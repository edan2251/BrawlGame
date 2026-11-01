using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Knockbox.Simulation;
using NUnit.Framework;

namespace Knockbox.Tests.EditMode
{
    /// <summary>
    /// 체크섬이 상태의 모든 필드를 덮는지 반사로 확인한다(시뮬레이션은 반사를 쓰지 않고, 테스트만 쓴다).
    /// 상태에 필드를 추가하고 Visit에 빠뜨리면 여기서 실패한다.
    /// </summary>
    public class StateChecksumTests
    {
        static readonly BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static bool IsLeaf(Type t) => t.IsPrimitive || t.IsEnum;

        /// <summary>박싱된 값의 말단 필드를 하나 바꾸는 함수 목록.</summary>
        static IEnumerable<(string path, Func<object, object> mutate)> Leaves(Type type, string prefix)
        {
            foreach (var field in type.GetFields(InstanceFields))
            {
                string path = prefix + field.Name;
                if (IsLeaf(field.FieldType))
                {
                    var f = field;
                    yield return (path, boxed =>
                    {
                        f.SetValue(boxed, Change(f.GetValue(boxed)));
                        return boxed;
                    });
                }
                else if (field.FieldType.IsValueType)
                {
                    var f = field;
                    foreach (var (childPath, childMutate) in Leaves(field.FieldType, path + "."))
                    {
                        yield return (childPath, boxed =>
                        {
                            object child = f.GetValue(boxed);
                            f.SetValue(boxed, childMutate(child));
                            return boxed;
                        });
                    }
                }
                else
                {
                    throw new InvalidOperationException($"{path}: 상태에 참조 형식 필드가 있음({field.FieldType.Name})");
                }
            }
        }

        static object Change(object value)
        {
            switch (value)
            {
                case float f: return f + 1.5f;
                case int i: return i + 1;
                case uint u: return u + 1u;
                case sbyte s: return (sbyte)(s == sbyte.MaxValue ? 0 : s + 1);
                case byte b: return (byte)(b + 1);
                case bool flag: return !flag;
                case Enum e:
                    var underlying = Convert.ToInt64(e);
                    return Enum.ToObject(e.GetType(), underlying + 1);
                default: throw new NotSupportedException(value.GetType().Name);
            }
        }

        [Test]
        public void Checksum_ChangesWhenAnyStateFieldChanges()
        {
            var baseState = TestWorlds.Create().State;
            uint baseChecksum = baseState.ComputeChecksum();
            var missing = new StringBuilder();
            int count = 0;

            foreach (var (path, mutate) in Leaves(typeof(SimState), ""))
            {
                count++;
                object boxed = baseState;
                var changed = (SimState)mutate(boxed);
                if (changed.ComputeChecksum() == baseChecksum)
                    missing.AppendLine(path);
            }

            Assert.Greater(count, 10);
            Assert.AreEqual(string.Empty, missing.ToString(), "체크섬에 빠진 필드");
        }

        [Test]
        public void Visit_ListsEveryLeafFieldOnce()
        {
            int leafCount = 0;
            foreach (var _ in Leaves(typeof(SimState), ""))
                leafCount++;
            var recorded = StateDiff.Record(TestWorlds.Create().State);
            Assert.AreEqual(leafCount, recorded.Names.Count, string.Join(", ", recorded.Names));
            Assert.AreEqual(recorded.Names.Count, new HashSet<string>(recorded.Names).Count, "이름이 겹치는 필드가 있음");
        }

        [Test]
        public void ConfigChecksum_ChangesWhenAnyParamChanges()
        {
            uint baseChecksum = SimConfig.CreateDefault().ComputeChecksum();
            var missing = new StringBuilder();
            foreach (var (path, mutate) in Leaves(typeof(MovementParams), "Movement."))
            {
                var m = (MovementParams)mutate(MovementParams.Default);
                if (new SimConfig(m, MatchParams.Default).ComputeChecksum() == baseChecksum)
                    missing.AppendLine(path);
            }
            foreach (var (path, mutate) in Leaves(typeof(MatchParams), "Match."))
            {
                var m = (MatchParams)mutate(MatchParams.Default);
                if (new SimConfig(MovementParams.Default, m).ComputeChecksum() == baseChecksum)
                    missing.AppendLine(path);
            }
            // 레거시 생성자는 일부 값(Swing.ForwardOnly)을 고정하므로 규칙 생성자로 확인한다(CP20).
            var rules = RuleSet.Legacy(CombatMode.Test, true);
            uint combatBase = new SimConfig(MovementParams.Default, MatchParams.Default, CombatParams.Default, rules).ComputeChecksum();
            foreach (var (path, mutate) in Leaves(typeof(CombatParams), "Combat."))
            {
                var c = (CombatParams)mutate(CombatParams.Default);
                if (new SimConfig(MovementParams.Default, MatchParams.Default, c, rules).ComputeChecksum() == combatBase)
                    missing.AppendLine(path);
            }
            Assert.AreEqual(string.Empty, missing.ToString(), "설정 체크섬에 빠진 필드");
        }

        [TestCase("", 0x811C9DC5u)]
        [TestCase("a", 0xE40C292Cu)]
        [TestCase("foobar", 0xBF9CF968u)]
        public void Hasher_MatchesFnv1aReferenceVectors(string text, uint expected)
        {
            var h = Hasher.Create();
            foreach (byte b in Encoding.ASCII.GetBytes(text))
                h.AddByte(b);
            Assert.AreEqual(expected, h.Hash);
        }

        [Test]
        public void Hasher_UsesFloatBitPattern()
        {
            var a = Hasher.Create();
            a.AddFloat(0f);
            var b = Hasher.Create();
            b.AddFloat(-0f);
            Assert.AreNotEqual(a.Hash, b.Hash, "+0과 -0은 비트가 다르므로 체크섬도 달라야 함");
        }
    }
}
