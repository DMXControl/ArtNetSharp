using System.Text.RegularExpressions;

namespace ArtNetSharp
{
    public readonly struct NodeReport
    {
        public readonly ENodeReportCodes ReportCode;
        public readonly uint Counter;
        public readonly string Text;

        private const string REGEX = "#([A-Fa-f0-9]+) \\[([0-9]+)\\](.*)";

        public NodeReport(in ENodeReportCodes reportCode, in string text = "", in uint counter = 0)
        {
            ReportCode = reportCode;
            Counter = counter;
            Text = text;
        }
        public NodeReport(in string reportCode)
        {
            var Matches = Regex.Matches(reportCode, REGEX);
            if (Matches.Count == 0)
            {
                ReportCode = 0;
                Counter = 0;
                Text = string.Empty;
                return;
            }
            string hex = Matches[0].Groups[1].Value.Replace(" ", "");
            string counter = Matches[0].Groups[2].Value.Replace(" ", "");
            string text = Matches[0].Groups[3].Value;
            if (text.StartsWith(" "))
                text = text.Substring(1);

            ReportCode = (ENodeReportCodes)ushort.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            Counter = uint.Parse(counter);
            Text = text;
        }

        public NodeReport Increment()
        {
            return new NodeReport(this.ReportCode, this.Text, this.Counter + 1);
        }

        public override bool Equals(object obj)
        {
            return obj is NodeReport other &&
                   ReportCode == other.ReportCode &&
                   Counter == other.Counter &&
                   Text == other.Text;
        }

        public override int GetHashCode()
        {
            int hashCode = -971048872;
            hashCode = hashCode * -1521134295 + ReportCode.GetHashCode();
            hashCode = hashCode * -1521134295 + Counter.GetHashCode();
            hashCode = hashCode * -1521134295 + Text.GetHashCode();
            return hashCode;
        }
        public override string ToString()
        {
            return $"#{(ushort)ReportCode:x4} [{Counter}] {Text}";
        }
    }
}