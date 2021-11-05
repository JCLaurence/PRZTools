using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCC.PRZTools
{
    public class NatElement
    {
        public NatElement()
        {

        }

        private int _elementID;

        public int ElementID 
        {
            get => _elementID;
            set
            {
                if (value > 99999 || value < 1)
                {
                    throw new Exception("Element ID out of range (1 to 99999)");
                }

                _elementID = value;
                ElementTable = PRZConstants.c_TABLE_NAT_PREFIX_ELEMENT + value.ToString("D5");
            }
        }

        public string ElementName { get; set; }

        public int ElementType { get; set; }

        public int ElementStatus { get; set; }

        public string ElementDataPath { get; set; }

        public int ThemeID { get; set; }

        public string ThemeName { get; set; }

        public string ThemeCode { get; set; }

        public string ElementTable { get; private set; }

        public int Presence { get; set; }

    }
}

