using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml;
using System.Xml.XPath;

namespace Warp
{
    public class DataBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string fieldName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(fieldName));
        }
    }

    public delegate void NotifiedPropertyChanged(object sender, object newValue);
}
