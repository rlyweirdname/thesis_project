using UnityEngine;

namespace Combat
{
    public enum HitType
    {
        Light,
        Heavy,
        Launcher,
        Grab
    }

    [System.Serializable]
    public struct HitData
    {
        public int damage;
        public HitType hitType;
        public bool causesStagger;

        public HitData(int damage, HitType hitType, bool causesStagger)
        {
            this.damage = damage;
            this.hitType = hitType;
            this.causesStagger = causesStagger;
        }
    }

    public interface IHitReceiver
    {
        void ReceiveHit(HitData hit, GameObject attacker);
    }
}
