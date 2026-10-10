namespace GtaSkylines.Mod.Rendering
{
    /// <summary>Lifetimes and fades of transient effects.</summary>
    public static class EffectMath
    {
        /// <summary>Tracer lifetime, seconds.</summary>
        public const float TracerLifetime = 0.06f;
        /// <summary>Impact puff lifetime, seconds.</summary>
        public const float ImpactLifetime = 0.5f;
        /// <summary>Explosion flash lifetime, seconds.</summary>
        public const float FlashLifetime = 0.4f;
        /// <summary>Explosion smoke lifetime, seconds.</summary>
        public const float SmokeLifetime = 4f;
        /// <summary>Fire and smoke column on a wreck, seconds.</summary>
        public const float WreckFireLifetime = 20f;

        /// <summary>Tracer opacity.</summary>
        public static float TracerAlpha(float age)
        {
            return age >= 0f && age < TracerLifetime ? 1f - age / TracerLifetime : 0f;
        }

        /// <summary>Impact puff size (m) and opacity.</summary>
        public static void Impact(float age, out float scale, out float alpha)
        {
            Fade(age, ImpactLifetime, 0.15f, 0.45f, 0.8f, out scale, out alpha);
        }

        /// <summary>Explosion flash size and opacity.</summary>
        public static void ExplosionFlash(float age, out float scale, out float alpha)
        {
            Fade(age, FlashLifetime, 2f, 6f, 1f, out scale, out alpha);
        }

        /// <summary>Explosion smoke size and opacity.</summary>
        public static void ExplosionSmoke(float age, out float scale, out float alpha)
        {
            Fade(age, SmokeLifetime, 3f, 5f, 0.6f, out scale, out alpha);
        }

        /// <summary>True while a wreck still burns.</summary>
        public static bool WreckBurning(float secondsSinceExplosion)
        {
            return secondsSinceExplosion >= 0f && secondsSinceExplosion < WreckFireLifetime;
        }

        private static void Fade(float age, float life, float scale0, float grow, float alpha0, out float scale, out float alpha)
        {
            if (age < 0f || age >= life)
            {
                scale = 0f;
                alpha = 0f;
                return;
            }
            float t = age / life;
            scale = scale0 + grow * t;
            alpha = alpha0 * (1f - t);
        }
    }
}
