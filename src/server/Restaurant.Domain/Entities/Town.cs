﻿using System.Collections.Generic;

namespace Restaurant.Domain.Entities
{
    public class Town
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public virtual ICollection<Restaurant> Restaurants { get; set; } = new HashSet<Restaurant>();
    }
}