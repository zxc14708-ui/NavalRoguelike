using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Enemies;

namespace Game.View
{
    public partial class ShipWreck
    {
        private static ShipWreck s_finale;
        private ParticleSystem[] _finaleParticles;
        private readonly List<Transform> _breakaway = new();
        private Transform _liftedHull, _skirt;
        private Vector3 _skirtScale, _skirtUp;
        private float _stage4Lift, _contactTime;
        private bool _contact;
        private float _stage4Side;

        private bool IsStage4Motion => Mode >= SinkMode.WigCrash;
        public bool HitSurface => _contact;
        public static bool PlayingFinale => s_finale != null && s_finale.Age < s_finale.Duration;
        public static Transform FinaleTarget => PlayingFinale ? s_finale.transform : null;

        public static bool TrySpawnStage4(EnemyController enemy, float speed, DamageSource? cause, Vector3 impact)
        {
            SinkMode? mode = enemy switch
            {
                LunBoss => SinkMode.LunCrash,
                WigEnemy or LightWig => SinkMode.WigCrash,
                Hovercraft => SinkMode.CushionCollapse,
                Hydrofoil => SinkMode.FoilCollapse,
                PlatformBattery => SinkMode.DeckCollapse,
                _ => null,
            };
            // The corvette uses the existing ship motions with its separate upper deck and turrets.
            if (enemy is MissileCorvette && cause is DamageSource.Gun or DamageSource.Missile or DamageSource.CookOff)
                return Spawn(enemy.transform, speed, cause, impact,
                    cause == DamageSource.Gun ? SinkMode.Superstructure : SinkMode.Magazine) != null;
            if (!mode.HasValue) return false;

            float lift = enemy is Hydrofoil foil ? foil.Lift : 0f;
            if (enemy is AirEnemy aircraft) speed = aircraft.Velocity.magnitude;
            return Spawn(enemy.transform, enemy.DevFrozen ? 0f : speed, cause, impact, mode, lift) != null;
        }

        private void BindStage4Copy(SinkMode mode, Transform source, Transform copy)
        {
            string part = source.name;
            if (mode == SinkMode.FoilCollapse && Under(source, "HullPivot"))
            {
                if (_liftedHull == null)
                {
                    _liftedHull = new GameObject("Raised hull").transform;
                    _liftedHull.SetParent(_body, false);
                }
                copy.SetParent(_liftedHull, true);
            }
            if (mode == SinkMode.CushionCollapse && part == "Skirt")
            {
                _skirt = copy;
                _skirtScale = copy.localScale;
                _skirtUp = copy.InverseTransformDirection(Vector3.up);
                _skirtUp = new Vector3(Mathf.Abs(_skirtUp.x), Mathf.Abs(_skirtUp.y), Mathf.Abs(_skirtUp.z));
            }

            bool detach = mode switch
            {
                SinkMode.LunCrash => part == "Wing_Port" || part == "Engine_S4" || part == "Engine_P4",
                SinkMode.WigCrash => part is "Wing_Port" or "Propeller" or "TailPropeller",
                SinkMode.CushionCollapse => part is "FanBlade_02" or "RocketLauncher_Port_Tubes",
                SinkMode.FoilCollapse => part is "MissileLauncher_Port" or "RadarArray",
                SinkMode.DeckCollapse => part is "TurretPivot_Housing" or "MissileBox_Port" or "RadarArray",
                _ => false,
            };
            if (detach) _breakaway.Add(copy);
        }

        private static bool Under(Transform part, string name)
        {
            for (Transform t = part; t != null; t = t.parent)
                if (t.name == name) return true;
            return false;
        }

        private void BeginStage4()
        {
            _stage4Side = _yawRate < 0f ? -1f : 1f;
            _duration = Mode switch
            {
                SinkMode.LunCrash => 4.1f,
                SinkMode.WigCrash => _length > 10f ? 3.3f : 2.4f,
                SinkMode.CushionCollapse => 3.4f,
                SinkMode.FoilCollapse => 3f,
                _ => 3.6f,
            };
            _speed = Mathf.Clamp(_speed, 0f, Mode == SinkMode.LunCrash ? 15f : 10f);
            _contactTime = Mode is SinkMode.LunCrash or SinkMode.WigCrash
                ? Mathf.Clamp(0.32f + Mathf.Sqrt(Mathf.Max(0f, _startPos.y - WaterY)) * 0.34f, .32f, 1.15f)
                : .42f;
            _depth = Mode switch
            {
                SinkMode.LunCrash => 14f,
                SinkMode.WigCrash => _length > 10f ? 6.5f : 3.3f,
                SinkMode.CushionCollapse => 6.7f,
                SinkMode.FoilCollapse => 5.6f,
                _ => 0f,
            };

            var blast = DeckPoint(_blastAlong);
            Explosions.Spawn(blast, Mode == SinkMode.LunCrash ? 1.85f : .62f * _scale,
                _startPos.y > WaterY + 1.5f && Mode != SinkMode.DeckCollapse
                    ? Explosions.Kind.Air : Explosions.Kind.Impact, false);
            if (Mode == SinkMode.DeckCollapse)
            {
                BreakParts(blast, .46f);
                SpawnChunks(blast, 3, .42f);
            }
        }

        private void TickStage4(float dt)
        {
            if (Mode == SinkMode.DeckCollapse)
            {
                _body.localRotation = Quaternion.Euler(7f * Mathf.SmoothStep(0f, 1f, Age / .25f), 0f, 0f);
                EmitSmoke(dt, 17f * (1f - Mathf.Clamp01(Age / Duration)), DeckPoint(0f), .85f);
                if (Age < .75f) EmitFire(dt, 8f);
            }
            else
            {
                bool wig = Mode is SinkMode.LunCrash or SinkMode.WigCrash;
                float u = Mathf.Clamp01((Age - _contactTime) / (Duration - _contactTime));
                float approach = Mathf.Clamp01(Age / _contactTime);
                float bank = Mathf.SmoothStep(0f, 1f, approach);
                float settle = Mathf.SmoothStep(0f, 1f, u);
                _speed *= Mathf.Exp(-dt / (Age < _contactTime ? 1.4f : .46f));
                _startPos += _startRot * Vector3.forward * (_speed * dt);
                Vector3 at = _startPos;
                at.y = Mathf.Lerp(_startPos.y, WaterY, approach * approach) - _depth * settle;
                float roll = Mode switch
                {
                    SinkMode.LunCrash => Mathf.Lerp(25f * bank, 52f, settle),
                    SinkMode.WigCrash => Mathf.Lerp(37f * bank, 77f, settle),
                    SinkMode.CushionCollapse => Mathf.Lerp(8f * bank, 41f, settle),
                    _ => Mathf.Lerp(18f * bank, 70f, settle),
                };
                float pitch = Mathf.Lerp((wig ? 14f : 8f) * bank, wig ? 38f : 23f, settle);
                RollDegrees = roll * _stage4Side;
                _sinkY = at.y - WaterY;
                transform.SetPositionAndRotation(at, _startRot * Quaternion.Euler(pitch, _stage4Side * 9f * settle, RollDegrees));

                if (_liftedHull != null) _liftedHull.localPosition = Vector3.down * (_stage4Lift * bank);
                if (_skirt != null) _skirt.localScale = Vector3.Scale(_skirtScale, Vector3.one - _skirtUp * (.53f * bank));

                if (!_contact && Age >= _contactTime)
                {
                    _contact = true;
                    Vector3 splash = WaterlinePoint(wig ? .24f : .13f, 0f);
                    Explosions.Spawn(splash, Mathf.Clamp(_scale * .68f, .45f, 1.5f), Explosions.Kind.Water, false);
                    OceanSurface.SpawnSinkingTrace(splash);
                    Vector3 fire = DeckPoint(wig ? .12f : 0f);
                    BreakParts(fire, wig ? .85f : .6f);
                    SpawnChunks(fire, Mode == SinkMode.LunCrash ? 5 : 3, .55f);
                    Sound(Game.Data.SfxId.WreckBlast, .76f);
                }
                if (_contact && u < .58f) EmitWaterline(dt, Mode == SinkMode.LunCrash ? 36f : 18f, 1.1f);
                if (u < .68f) EmitSmoke(dt, _scale * (1f - u) * 18f, DeckPoint(wig ? -.28f : 0f), 1f);
                if (Age >= _contactTime && u < .45f) EmitFire(dt, 8f * _scale);
            }

            UpdateFlying(dt);
            if (Age < Duration) return;
            EndFinale();
            Active.Remove(this);
            Destroy(gameObject);
        }

        private void BreakParts(Vector3 at, float power)
        {
            foreach (var part in _breakaway)
            {
                if (part == null) continue;
                part.SetParent(null, true);
                Launch(part, at, power);
            }
            _breakaway.Clear();
        }

        public static void BeginFinale()
        {
            if (PlayingFinale) return;
            var wreck = Active.FindLast(w => w != null && w.Mode == SinkMode.LunCrash && w.Age < .25f);
            if (wreck == null) return;
            s_finale = wreck;
            wreck._finaleParticles = new[] { DecorFx.WreckSmoke, DecorFx.Fire, DecorFx.Spray,
                                             DecorFx.Sparks, DecorFx.Flash, DecorFx.BlastFire };
            wreck.SetFinaleParticleClock(true);
        }

        private void SetFinaleParticleClock(bool realtime)
        {
            if (_finaleParticles == null) return;
            foreach (var ps in _finaleParticles)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.useUnscaledTime = realtime;
            }
        }

        private void EndFinale()
        {
            if (s_finale != this) return;
            SetFinaleParticleClock(false);
            s_finale = null;
        }
    }
}

