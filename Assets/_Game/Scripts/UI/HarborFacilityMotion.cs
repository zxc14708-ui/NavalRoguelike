using UnityEngine;

namespace Game.UI
{
    /// <summary>확대 중인 연구소·기록실의 소규모 장비만 움직인다.</summary>
    public sealed class HarborFacilityMotion
    {
        private enum Focus { None, Research, Records }
        private readonly Transform _research, _records, _radar, _fan, _indicator;
        private readonly Quaternion _radarStart, _fanStart;
        private readonly Vector3 _indicatorStart;
        private Focus _focus;
        private float _researchTime, _recordsTime;

        public HarborFacilityMotion(Transform details)
        {
            _research = details != null ? details.Find("Research") : null;
            _records = details != null ? details.Find("Records") : null;
            _radar = _research != null ? _research.Find("ResearchRadar") : null;
            _fan = _records != null ? _records.Find("RecordsFan") : null;
            _indicator = _records != null ? _records.Find("RecordsIndicator") : null;
            _radarStart = _radar != null ? _radar.localRotation : Quaternion.identity;
            _fanStart = _fan != null ? _fan.localRotation : Quaternion.identity;
            _indicatorStart = _indicator != null ? _indicator.localPosition : Vector3.zero;
        }

        private static bool Matches(Transform facility, Vector3 point)
            => facility != null && Mathf.Abs(point.x - facility.localPosition.x) < .1f
                && Mathf.Abs(point.z - facility.localPosition.z) < .1f;

        public void FocusAt(Vector3 point)
            => _focus = Matches(_research, point) ? Focus.Research : Matches(_records, point) ? Focus.Records : Focus.None;

        public void Stop() => _focus = Focus.None;

        /// <summary>일시정지 및 배경 정지 중에는 호출자가 0을 전달한다.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (_focus == Focus.Research)
            {
                _researchTime = (_researchTime + dt) % 18f;
                if (_radar != null) _radar.localRotation = _radarStart * Quaternion.Euler(0f, _researchTime * 20f, 0f);
            }
            else if (_focus == Focus.Records)
            {
                _recordsTime = (_recordsTime + dt) % 24f;
                if (_fan != null) _fan.localRotation = _fanStart * Quaternion.Euler(0f, _recordsTime * 60f, 0f);
                if (_indicator != null) _indicator.localPosition = _indicatorStart + Vector3.right * (2.1f * Mathf.Sin(_recordsTime * Mathf.PI / 2f));
            }
        }
    }
}
