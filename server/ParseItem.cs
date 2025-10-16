using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSCodeLanguageServer
{
   public class ParseItem
   {
      public ParseItem(string item, string leadingDelimiter, int itemPosition, int itemLine)
      {
         Item = item;
         LeadingDelimiter = leadingDelimiter;
         ItemPosition = itemPosition;
         ItemLine = itemLine;
      }

      public int ItemPosition{  get; private set; }
      public int ItemLine{  get; private set; }
      public string Item { get; private set; }
      public string LeadingDelimiter { get; private set; }
   }
}
