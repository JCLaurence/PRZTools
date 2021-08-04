using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NCC.PRZTools
{
    public class ProgressManager: INotifyPropertyChanged
    {
        private ProgressManager()
        {
        }

        // factory method
        public static ProgressManager CreateProgressManager(int prog_max)
        {
            return new ProgressManager
            {
                _min = 0,
                _max = prog_max,
                _current = 0,
                _message = ""
            };
        }

        // implement INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.  
        // The CallerMemberName attribute that is applied to the optional propertyName  
        // parameter causes the property name of the caller to be substituted as an argument.  
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Properties
        private int _min;
        private int _max;
        private int _current;
        private string _message;

        public int Min
        {
            get { return _min; }

            set
            {
                if (value != _min)
                {
                    _min = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int Max
        {
            get { return _max; }

            set
            {
                if (value != _max)
                {
                    _max = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int Current
        {
            get { return _current; }

            set
            {
                if (value != _current)
                {
                    _current = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Message
        {
            get { return _message; }

            set
            {
                if (value != _message)
                {
                    _message = value;
                    NotifyPropertyChanged();
                }
            }
        }

    }
}
