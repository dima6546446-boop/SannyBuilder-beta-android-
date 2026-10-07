using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    /// <summary>
    /// Drift points: angle x speed x time with a growing combo multiplier, direction-change (transition) bonus,
    /// and a penalty/chain-break on collisions. One per car (player and AI battle opponents).
    /// </summary>
    public class DriftScorer : MonoBehaviour
    {
        public float minAngle = 12f;
        public float minSpeedMs = 9f;
        public float grace = 0.9f;
        public float maxMultiplier = 10f;
        public bool ScoringEnabled = true;
        public bool RaiseGlobalEvents;
        public int zoneDepth;                 // >0 while inside a scoring zone
        public float zoneMult = 1f;
        public float EffectiveZoneMultiplier { get { return zoneDepth > 0 ? zoneMult : 1f; } }

        public float ChainScore { get; private set; }
        public float Multiplier { get; private set; }
        public float ChainTime { get; private set; }
        public float BankedScore { get; private set; }
        public float BestChain { get; private set; }
        public float BestMultiplier { get; private set; }
        public bool Active { get; private set; }
        public float TotalScore { get { return BankedScore + ChainScore; } }
        public int TransitionCount { get; private set; }

        public event System.Action<int, float, float> ChainBanked;
        public event System.Action<float> ChainLost;
        public event System.Action<int> TransitionScored;

        private VehicleController vc;
        private float lostTimer;
        private float lastSign;
        private float maxMulThisChain = 1f;
        private float lastCollisionTime = -10f;

        public void Init(VehicleController v)
        {
            vc = v;
            Multiplier = 1f;
            vc.Collided += OnCollision;
        }

        private void OnDestroy() { if (vc != null) vc.Collided -= OnCollision; }

        public void ResetAll()
        {
            Active = false; ChainScore = 0f; Multiplier = 1f; ChainTime = 0f; BankedScore = 0f; BestChain = 0f; BestMultiplier = 1f;
            TransitionCount = 0; lostTimer = 0f;
        }

        public void AddBonus(int pts) { BankedScore += pts; }

        public void SetEnabled(bool on)
        {
            if (!on && Active) Bank();
            ScoringEnabled = on;
        }

        private void FixedUpdate()
        {
            if (vc == null || vc.Wheels == null) return;
            float dt = Time.fixedDeltaTime;
            bool drifting = ScoringEnabled && vc.IsGrounded && vc.SpeedMs > minSpeedMs && vc.AbsDriftAngle > minAngle && vc.ForwardSpeed > 0f
                && (vc.RearSlip > 0.2f || vc.FrontSlip > 0.35f);

            if (drifting)
            {
                if (!Active) { Active = true; ChainScore = 0f; ChainTime = 0f; Multiplier = 1f; maxMulThisChain = 1f; lastSign = Mathf.Sign(vc.DriftAngle); }
                lostTimer = 0f;
                ChainTime += dt;
                float ang = vc.AbsDriftAngle;
                float angleF = Mathf.Clamp01((ang - minAngle) / 42f);
                float speedF = Mathf.Clamp01((vc.SpeedKmh - 30f) / 100f);
                float rate = 40f + 520f * angleF * (0.3f + 0.7f * speedF) + 120f * speedF;
                ChainScore += rate * dt * Multiplier * EffectiveZoneMultiplier;
                Multiplier = Mathf.Min(maxMultiplier, Multiplier + dt * (0.10f + 0.25f * angleF));
                maxMulThisChain = Mathf.Max(maxMulThisChain, Multiplier);

                float sign = Mathf.Sign(vc.DriftAngle);
                if (sign != lastSign && ang > minAngle + 4f)
                {
                    lastSign = sign;
                    TransitionCount++;
                    int bonus = Mathf.RoundToInt(250f * Multiplier * 0.5f);
                    ChainScore += bonus;
                    Multiplier = Mathf.Min(maxMultiplier, Multiplier + 0.6f);
                    if (TransitionScored != null) TransitionScored(bonus);
                }
                BestChain = Mathf.Max(BestChain, ChainScore);
                BestMultiplier = Mathf.Max(BestMultiplier, Multiplier);
            }
            else if (Active)
            {
                lostTimer += dt;
                if (lostTimer > grace) Bank();
            }
        }

        private void Update()
        {
            if (RaiseGlobalEvents && vc != null)
            {
                GameEvents.RaiseDriftUpdated(new DriftSnapshot
                {
                    active = Active, chainScore = ChainScore, multiplier = Multiplier, angle = vc.AbsDriftAngle,
                    speedKmh = vc.SpeedKmh, chainTime = ChainTime
                });
            }
        }

        private void Bank()
        {
            int pts = Mathf.RoundToInt(ChainScore);
            Active = false;
            if (pts >= 150)
            {
                BankedScore += pts;
                if (ChainBanked != null) ChainBanked(pts, maxMulThisChain, ChainTime);
                if (RaiseGlobalEvents) GameEvents.RaiseDriftBanked(pts, maxMulThisChain, ChainTime);
            }
            ChainScore = 0f; ChainTime = 0f; Multiplier = 1f; lostTimer = 0f;
        }

        private void OnCollision(Collision c)
        {
            if (!Active) return;
            float impulse = c.impulse.magnitude;
            if (impulse < 1800f || Time.time - lastCollisionTime < 0.4f) return;
            lastCollisionTime = Time.time;
            var tag = c.collider.GetComponentInParent<CollisionTag>();
            if (tag != null && tag.isSoft) { ChainScore *= 0.9f; return; }
            float lost = ChainScore * (impulse > 9000f ? 1f : 0.5f);
            ChainScore -= lost;
            Multiplier = 1f;
            if (ChainScore < 150f) { Active = false; ChainScore = 0f; ChainTime = 0f; }
            if (ChainLost != null) ChainLost(lost);
            if (RaiseGlobalEvents) GameEvents.RaiseDriftPenalty(lost);
        }
    }
}
