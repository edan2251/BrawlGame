using System;
using System.Collections.Generic;
using System.IO;

namespace Knockbox.Simulation
{
    /// <summary>
    /// 경기 입력 기록(01-architecture.md 6절). 양쪽 입력, 맵, 모드, 설정 체크섬, 시작 상태를 만드는 값,
    /// 시작·최종 체크섬을 담는다. 재생 결과의 최종 체크섬이 기록 시점과 같아야 한다.
    /// </summary>
    public sealed class InputRecording
    {
        public const uint Magic = 0x5052424B; // "KBRP"
        public const ushort FormatVersion = 1;

        readonly List<InputFrame> p1 = new List<InputFrame>();
        readonly List<InputFrame> p2 = new List<InputFrame>();

        public uint ConfigChecksum;
        public uint Seed;
        public Vec2 Spawn0;
        public Vec2 Spawn1;
        public MatchPhase StartPhase;
        public string Arena = "";
        public string Mode = "";
        public uint InitialChecksum;
        public bool HasFinalChecksum;
        public uint FinalChecksum;

        public int Count => p1.Count;
        public InputFrame GetP1(int index) => p1[index];
        public InputFrame GetP2(int index) => p2[index];

        public static InputRecording Begin(SimConfig config, uint seed, Vec2 spawn0, Vec2 spawn1, MatchPhase startPhase, string arena, string mode)
        {
            var rec = new InputRecording
            {
                ConfigChecksum = config.ComputeChecksum(),
                Seed = seed,
                Spawn0 = spawn0,
                Spawn1 = spawn1,
                StartPhase = startPhase,
                Arena = arena ?? "",
                Mode = mode ?? "",
            };
            rec.InitialChecksum = SimState.CreateRound(config, seed, spawn0, spawn1, startPhase).ComputeChecksum();
            return rec;
        }

        public void Append(in InputFrame a, in InputFrame b)
        {
            p1.Add(a);
            p2.Add(b);
        }

        public void Finish(uint finalChecksum)
        {
            FinalChecksum = finalChecksum;
            HasFinalChecksum = true;
        }

        /// <summary>기록의 시작 상태를 다시 만든다. 설정이나 시작 상태가 기록과 다르면 예외.</summary>
        public SimState CreateInitialState(SimConfig config)
        {
            uint configChecksum = config.ComputeChecksum();
            if (configChecksum != ConfigChecksum)
                throw new InvalidOperationException($"설정 체크섬이 기록과 다름: {ConfigChecksum:X8} → {configChecksum:X8}");
            var state = SimState.CreateRound(config, Seed, Spawn0, Spawn1, StartPhase);
            uint initial = state.ComputeChecksum();
            if (initial != InitialChecksum)
                throw new InvalidOperationException($"시작 상태 체크섬이 기록과 다름: {InitialChecksum:X8} → {initial:X8}");
            return state;
        }

        /// <summary>새 SimWorld에서 처음부터 끝까지 재생하고 최종 체크섬을 돌려준다.</summary>
        public uint Replay(SimConfig config)
        {
            var world = new SimWorld(config, CreateInitialState(config));
            for (int i = 0; i < p1.Count; i++)
                world.Step(p1[i], p2[i]);
            return world.ComputeChecksum();
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic);
                w.Write(FormatVersion);
                w.Write(ConfigChecksum);
                w.Write(Seed);
                w.Write(BitConverter.SingleToInt32Bits(Spawn0.X));
                w.Write(BitConverter.SingleToInt32Bits(Spawn0.Y));
                w.Write(BitConverter.SingleToInt32Bits(Spawn1.X));
                w.Write(BitConverter.SingleToInt32Bits(Spawn1.Y));
                w.Write((byte)StartPhase);
                w.Write(Arena);
                w.Write(Mode);
                w.Write(InitialChecksum);
                w.Write(HasFinalChecksum);
                w.Write(FinalChecksum);
                w.Write(p1.Count);
                var buffer = new byte[6];
                for (int i = 0; i < p1.Count; i++)
                {
                    p1[i].Write(buffer, 0);
                    p2[i].Write(buffer, 3);
                    w.Write(buffer);
                }
            }
            return stream.ToArray();
        }

        public static InputRecording FromBytes(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            using var r = new BinaryReader(stream);
            if (r.ReadUInt32() != Magic)
                throw new InvalidDataException("입력 기록 파일이 아님");
            ushort version = r.ReadUInt16();
            if (version != FormatVersion)
                throw new InvalidDataException($"지원하지 않는 입력 기록 버전: {version}");

            var rec = new InputRecording
            {
                ConfigChecksum = r.ReadUInt32(),
                Seed = r.ReadUInt32(),
                Spawn0 = new Vec2(BitConverter.Int32BitsToSingle(r.ReadInt32()), BitConverter.Int32BitsToSingle(r.ReadInt32())),
                Spawn1 = new Vec2(BitConverter.Int32BitsToSingle(r.ReadInt32()), BitConverter.Int32BitsToSingle(r.ReadInt32())),
                StartPhase = (MatchPhase)r.ReadByte(),
                Arena = r.ReadString(),
                Mode = r.ReadString(),
                InitialChecksum = r.ReadUInt32(),
                HasFinalChecksum = r.ReadBoolean(),
                FinalChecksum = r.ReadUInt32(),
            };
            int count = r.ReadInt32();
            if (count < 0)
                throw new InvalidDataException($"잘못된 프레임 수: {count}");
            for (int i = 0; i < count; i++)
            {
                byte[] buffer = r.ReadBytes(6);
                if (buffer.Length != 6)
                    throw new InvalidDataException("입력 기록이 중간에 끊김");
                rec.Append(InputFrame.Read(buffer, 0), InputFrame.Read(buffer, 3));
            }
            return rec;
        }
    }
}
