using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YemenGuide.Core.Entities;

namespace YemenGuide.Core.DTOs.Admin
{
    public class PlacesDto
    {


        public IEnumerable<Place> AdminPlaces { get; set; } = new List<Place>();
        public IEnumerable<Place> PendingPlaces { get; set; } = new List<Place>();
        public IEnumerable<Place> ActivePlaces { get; set; } = new List<Place>();
        public IEnumerable<Place> RejectedPlaces { get; set; } = new List<Place>();
        public IEnumerable<Place> SuspendedPlaces { get; set; } = new List<Place>();
        public IEnumerable<Place> WaitingOwnerApprovalPlaces { get; set; } = new List<Place>();

    }
}
