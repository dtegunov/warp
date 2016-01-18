using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace Warp.Tools
{
    public static class XMLHelper
    {
        public static void WriteAttribute(XmlTextWriter writer, string name, string value)
        {
            writer.WriteStartAttribute(name);
            writer.WriteValue(value);
            writer.WriteEndAttribute();
        }

        public static void WriteAttribute(XmlTextWriter writer, string name, int value)
        {
            writer.WriteStartAttribute(name);
            writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndAttribute();
        }

        public static void WriteAttribute(XmlTextWriter writer, string name, float value)
        {
            writer.WriteStartAttribute(name);
            writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndAttribute();
        }

        public static void WriteAttribute(XmlTextWriter writer, string name, double value)
        {
            writer.WriteStartAttribute(name);
            writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndAttribute();
        }

        public static void WriteAttribute(XmlTextWriter writer, string name, decimal value)
        {
            writer.WriteStartAttribute(name);
            writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndAttribute();
        }

        public static void WriteParamNode(XmlTextWriter writer, string name, string value)
        {
            writer.WriteStartElement("Param");
            XMLHelper.WriteAttribute(writer, "Name", name);
            XMLHelper.WriteAttribute(writer, "Value", value);
            writer.WriteEndElement();
        }

        public static void WriteParamNode(XmlTextWriter writer, string name, bool value)
        {
            writer.WriteStartElement("Param");
            XMLHelper.WriteAttribute(writer, "Name", name);
            XMLHelper.WriteAttribute(writer, "Value", value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        public static void WriteParamNode(XmlTextWriter writer, string name, int value)
        {
            writer.WriteStartElement("Param");
            XMLHelper.WriteAttribute(writer, "Name", name);
            XMLHelper.WriteAttribute(writer, "Value", value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        public static void WriteParamNode(XmlTextWriter writer, string name, long value)
        {
            writer.WriteStartElement("Param");
            XMLHelper.WriteAttribute(writer, "Name", name);
            XMLHelper.WriteAttribute(writer, "Value", value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        public static void WriteParamNode(XmlTextWriter writer, string name, float value)
        {
            writer.WriteStartElement("Param");
            XMLHelper.WriteAttribute(writer, "Name", name);
            XMLHelper.WriteAttribute(writer, "Value", value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        public static void WriteParamNode(XmlTextWriter writer, string name, double value)
        {
            writer.WriteStartElement("Param");
            XMLHelper.WriteAttribute(writer, "Name", name);
            XMLHelper.WriteAttribute(writer, "Value", value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        public static void WriteParamNode(XmlTextWriter writer, string name, decimal value)
        {
            writer.WriteStartElement("Param");
            XMLHelper.WriteAttribute(writer, "Name", name);
            XMLHelper.WriteAttribute(writer, "Value", value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        public static string LoadParamNode(XPathNavigator nav, string name, string defaultValue)
        {
            XPathNodeIterator Iterator = nav.Select($"//Param[@Name = \"{name}\"]");
            if (Iterator.Count == 0)
                //throw new Exception();
                return defaultValue;

            Iterator.MoveNext();
            string Value = Iterator.Current.GetAttribute("Value", "");
            return Value;
        }

        public static bool LoadParamNode(XPathNavigator nav, string name, bool defaultValue)
        {
            XPathNodeIterator Iterator = nav.Select($"//Param[@Name = \"{name}\"]");
            if (Iterator.Count == 0)
                //throw new Exception();
                return defaultValue;

            Iterator.MoveNext();
            string Value = Iterator.Current.GetAttribute("Value", "");
            if (Value.Length > 0)
                try
                {
                    return bool.Parse(Value);
                }
                catch (Exception)
                { }

            return defaultValue;
        }

        public static int LoadParamNode(XPathNavigator nav, string name, int defaultValue)
        {
            XPathNodeIterator Iterator = nav.Select($"//Param[@Name = \"{name}\"]");
            if (Iterator.Count == 0)
                //throw new Exception();
                return defaultValue;

            Iterator.MoveNext();
            string Value = Iterator.Current.GetAttribute("Value", "");
            if (Value.Length > 0)
                try
                {
                    return int.Parse(Value, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                { }

            return defaultValue;
        }

        public static long LoadParamNode(XPathNavigator nav, string name, long defaultValue)
        {
            XPathNodeIterator Iterator = nav.Select($"//Param[@Name = \"{name}\"]");
            if (Iterator.Count == 0)
                //throw new Exception();
                return defaultValue;

            Iterator.MoveNext();
            string Value = Iterator.Current.GetAttribute("Value", "");
            if (Value.Length > 0)
                try
                {
                    return long.Parse(Value, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                { }

            return defaultValue;
        }

        public static float LoadParamNode(XPathNavigator nav, string name, float defaultValue)
        {
            XPathNodeIterator Iterator = nav.Select($"//Param[@Name = \"{name}\"]");
            if (Iterator.Count == 0)
                //throw new Exception();
                return defaultValue;

            Iterator.MoveNext();
            string Value = Iterator.Current.GetAttribute("Value", "");
            if (Value.Length > 0)
                try
                {
                    return float.Parse(Value, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                { }

            return defaultValue;
        }

        public static double LoadParamNode(XPathNavigator nav, string name, double defaultValue)
        {
            XPathNodeIterator Iterator = nav.Select($"//Param[@Name = \"{name}\"]");
            if (Iterator.Count == 0)
                //throw new Exception();
                return defaultValue;

            Iterator.MoveNext();
            string Value = Iterator.Current.GetAttribute("Value", "");
            if (Value.Length > 0)
                try
                {
                    return double.Parse(Value, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                { }

            return defaultValue;
        }

        public static decimal LoadParamNode(XPathNavigator nav, string name, decimal defaultValue)
        {
            XPathNodeIterator Iterator = nav.Select($"//Param[@Name = \"{name}\"]");
            if (Iterator.Count == 0)
                //throw new Exception();
                return defaultValue;

            Iterator.MoveNext();
            string Value = Iterator.Current.GetAttribute("Value", "");
            if (Value.Length > 0)
                try
                {
                    return decimal.Parse(Value, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                { }

            return defaultValue;
        }
    }
}
