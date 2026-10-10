namespace GtaSkylines.Game.City
{
    /// <summary>
    /// Picks citizen models by role, so cops wear the city's police uniforms and random civilians never wear
    /// a uniform or a mascot costume. Allocation-free; called at spawn time only.
    /// </summary>
    public static class CitizenModels
    {
        /// <summary>
        /// <paramref name="pick"/> when it is a civilian model, else the next civilian model after it (wrapping
        /// around). Returns <paramref name="pick"/> unchanged when it is out of range or the city has no civilian
        /// model.
        /// </summary>
        public static int Civilian(ICity city, int pick)
        {
            int count = city.CitizenModelCount;
            if (pick < 0 || pick >= count) return pick;
            for (int k = 0; k < count; k++)
            {
                int id = (pick + k) % count;
                CitizenModel m = city.GetCitizenModel(id);
                if (m != null && m.Role == CitizenModelRole.Civilian) return id;
            }
            return pick;
        }

        /// <summary>
        /// A police model chosen by <paramref name="salt"/> among the city's police models (the
        /// <c>salt % policeCount</c>-th in id order), or −1 when the city has none.
        /// </summary>
        public static int Police(ICity city, uint salt)
        {
            int count = city.CitizenModelCount;
            int police = 0;
            for (int id = 0; id < count; id++)
                if (IsPolice(city, id)) police++;
            if (police == 0) return -1;
            int nth = (int)(salt % (uint)police);
            for (int id = 0; id < count; id++)
            {
                if (!IsPolice(city, id)) continue;
                if (nth == 0) return id;
                nth--;
            }
            return -1;
        }

        private static bool IsPolice(ICity city, int id)
        {
            CitizenModel m = city.GetCitizenModel(id);
            return m != null && m.Role == CitizenModelRole.Police;
        }
    }
}
