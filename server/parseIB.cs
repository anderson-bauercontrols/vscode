using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSCodeLanguageServer
{
   public class ParseIB
   {
      private const string COMMENT_NAME = "comment";
      private const string ENDCOMMENT_NAME = "endcomment";
      private const string MODULE_NAME = "module";
      private const string LIBRARY_NAME = "library";
      private const string INCLUDE_NAME = "include";
      public static ParseIB Parse(string filePath, string contents)
      {
         List<ParseItem> items = new List<ParseItem>();
         string item = string.Empty;
         string leadingDelimiter = string.Empty;
         bool processingSemicolon = false;
         bool processingLineContinue = false;
         bool processedCR = false;
         bool processingComment = false;
         int itemLinePosition = -1;
         int lineCount = 0;

         // find the rootDirectory
         DirectoryInfo rootDirectory = null;
         {
            FileInfo fileInfo = new FileInfo(filePath);
            DirectoryInfo fileDirectory = fileInfo.Directory;
            DirectoryInfo testDirectory = fileDirectory;
            while (fileDirectory != null && rootDirectory == null)
            {
               if (fileDirectory.Name.ToLower() == "source")
               {
                  var directories = fileDirectory.GetDirectories();
                  foreach (DirectoryInfo directory in directories)
                  {
                     if (directory.Name.ToLower() == "system")
                     {
                        rootDirectory = fileDirectory;
                        break;
                     }
                  }
               }
               else
               {
                  fileDirectory = fileDirectory.Parent;
                  if (fileDirectory == fileDirectory.Root)
                  {
                     fileDirectory = null;
                  }
               }

            }
         }
         int linePosition = 0;
         for (int i = 0; i < contents.Length; i++)
         {
            char c = contents[i];
            if (c == '\n' || c == '\r')
            {
               linePosition = 0;
            }
            if ((c == '_')|| ('0' <= c && '9' >= c) || ('a' <= c && 'z' >= c) || ('A' <= c && 'Z' >= c) && !processingSemicolon)
            { // is alpha numeric
               if (!processingSemicolon)
               {
                  if (item.Length == 0)
                  {
                     itemLinePosition = linePosition;
                  }
                  item += c.ToString();
               }
            }
            else
            {  // delimiter
               if (processedCR && c == '\n')
               {
                  processedCR = false;
               }
               else
               {
                  if (processingSemicolon)
                  {
                     if (c == '\r')
                     {
                        processingSemicolon = false;
                        processedCR = true;
                        lineCount++;
                     }
                     item = string.Empty;
                  }
                  else if (string.Compare(item, COMMENT_NAME, true) == 0)
                  {
                     processingComment = true;
                     item = string.Empty;
                     leadingDelimiter = string.Empty;
                  }
                  else if (processingComment)
                  {
                     if (string.Compare(item, ENDCOMMENT_NAME, true) == 0)
                     {
                        processingComment = false;
                     }
                     item = string.Empty;
                     leadingDelimiter = string.Empty;
                  }
                  else if ((string.Compare(item, INCLUDE_NAME, true) == 0) && rootDirectory != null)
                  {  // handle include file
                     int save_i = i;
                     // grab line
                     while (c != '"' && c != '\'' && c != '\r' && c != '\n' && i < contents.Length)
                     {
                        i++;
                        c = contents[i];
                     }
                     string fileSpecifier = string.Empty;
                     if (c == '"' || c == '\'')
                     { // open quote found
                        i++;
                        c = contents[i];
                        while (c != '"' && c != '\'' && c != '\r' && c != '\n' && i < contents.Length)
                        {
                           fileSpecifier += c.ToString();
                           i++;
                           c = contents[i];
                        }
                        if (c == '"' || c == '\'')
                        { // close quote found
                           string includeFilePath = rootDirectory.FullName + "\\" + fileSpecifier;
                           FileInfo includeFileInfo = new FileInfo(includeFilePath);
                           if (includeFileInfo.Extension == string.Empty)
                           {
                              includeFilePath = includeFileInfo.FullName + ".ib";
                              includeFileInfo = new FileInfo(includeFilePath);
                           }
                           if (includeFileInfo.Exists)
                           {
                              using (FileStream fs = new FileStream(includeFilePath, FileMode.Open, FileAccess.Read))
                              {
                                 ParseIB parsedInclude = Parse(includeFilePath, fs);
                                 foreach(var includeItem in parsedInclude.ParseItems)
                                 {
                                    items.Add(new ParseItem(includeItem.Item, includeItem.LeadingDelimiter, -1, -1));
                                 }
                              }
                           }
                        }
                     }
                  }
                  else
                  {
                     if (processingLineContinue)
                     {
                        if (c == '\r')
                        {
                           processingLineContinue = false;
                           processedCR = true;
                           lineCount++;
                        }
                     }
                     else if (c == '\r')
                     {
                        if (item.Length > 0)
                        {
                           items.Add(new ParseItem(item, leadingDelimiter, itemLinePosition, lineCount));
                        }
                        item = string.Empty;
                        leadingDelimiter = string.Empty;
                        itemLinePosition = -1;
                        processedCR = true;
                        lineCount++;
                     }
                     else if (c == '\\')
                     {  // ignore until next cr/lf
                        processingLineContinue = true;
                     }
                     else
                     {
                        processedCR = false;
                        if (item.Length > 0)
                        {
                           items.Add(new ParseItem(item, leadingDelimiter, itemLinePosition, lineCount));
                           item = string.Empty;
                           leadingDelimiter = string.Empty;
                           itemLinePosition = -1;
                        }

                        string temporaryString = c.ToString();
                        if (c == ';')
                        {  // semicolon: ignore everything until end of line
                           processingSemicolon = true;
                        }
                        else
                        {
                           if (leadingDelimiter.Length == 0)
                           {
                              leadingDelimiter = temporaryString;
                           }
                           if (string.IsNullOrEmpty(temporaryString))
                           {

                           }
                           else
                           {
                              if (string.IsNullOrWhiteSpace(leadingDelimiter))
                              {
                                 leadingDelimiter = temporaryString;
                              }
                           }
                        }
                     }
                  }
               }
            }
            linePosition++;
         }


         return new ParseIB(items);
      }

      private ParseIB(List<ParseItem> items)
      {
         ParseItems = items;
      }

      public List<ParseItem> ParseItems { get; private set; }

      // for testing: reads contents of an IB file and applies parse
      public static ParseIB Parse(string filePath, FileStream fileStream)
      {
         using (BinaryReader reader = new BinaryReader(fileStream)) 
         {
            byte[] buffer = new byte[fileStream.Length];
            reader.Read(buffer, 0, buffer.Length);
            string contents = string.Empty;

            foreach(byte b in buffer)
            {
               contents += ((char)b).ToString();
            }
            return Parse(filePath, contents);
         }
      }

   }
}
