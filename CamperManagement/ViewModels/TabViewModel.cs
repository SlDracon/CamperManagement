using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.ViewModels
{
    public class TabViewModel
    {
        public object CamperView { get; }
        public object RechnungenView { get; }

        public TabViewModel(object camperView, object rechnungenView)
        {
            CamperView = camperView;
            RechnungenView = rechnungenView;
        }
    }
}
