using OxyPlot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace User.ActiveBeltTensioner
{
    public class DeviceViewModel : INotifyPropertyChanged
    {
        public DeviceSettings Settings {
            get { return _plugin.Settings; }
        }
        public MotorController MotorController {
            get { return _plugin.MotorController; }
        }
        public PlotModel TelemetryGraphModel {
            get { return _plugin.TelemetryGraphModel; }
        }

        private DevicePlugin _plugin;

        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    _plugin.SelectedTabIndex = value;
                    OnPropertyChanged(nameof(SelectedTabIndex));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public DeviceViewModel(DevicePlugin plugin)
        {
            _plugin = plugin;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
