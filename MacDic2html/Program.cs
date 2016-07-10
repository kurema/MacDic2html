using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;

using System.Text.RegularExpressions;


namespace MacDic2html
{
    class Program
    {
        static void Main(string[] args)
        {
            var cssParser = new ExCSS.Parser();
            ExCSS.StyleSheet Style;
            ExCSS.StyleSheet StyleDic;

            using (var sr = new System.IO.StreamReader("DefaultStyle.css", Encoding.Unicode))
            using (var sr2 = new System.IO.StreamReader("DictionaryStyle.css", Encoding.UTF8))
            {
                Style = cssParser.Parse(sr.ReadToEnd());
                StyleDic = cssParser.Parse(sr2.ReadToEnd());
            }

            System.IO.File.Delete("out.html");
            using (var sw = new System.IO.StreamWriter("out.html", true, Encoding.UTF8))
            {
                sw.Write("<html><head><title></title></head><body><dl>");
                var sbLinked = new StringBuilder();
                int k = 0;
                for (int i = 0; System.IO.File.Exists("out/" + i + ".xml"); i++)
                {
                    Console.Write(i + "...");
                    using (var sr = new System.IO.StreamReader("out/" + i + ".xml"))
                    {
                        var content = sr.ReadToEnd();
                        var matches = Regex.Matches(content, @"<d:entry ([^\<\>]*?)\>(.+?)<\/d:entry\>");
                        var sb = new StringBuilder();
                        foreach (Match match in matches)
                        {
                            string html;
                            string linked;
                            ConvertEntry(match.Value, Style, StyleDic, out html, out linked, ref k);
                            sb.Append(html );
                            sbLinked.Append(linked);
                        }
                        sw.Write(sb.ToString());
                    }
                }
                sw.Write(sbLinked.ToString());
            }
        }


        static void ConvertEntry(string content, ExCSS.StyleSheet Style, ExCSS.StyleSheet StyleDic,out string html,out string linked,ref int refCnt)
        {
            var sbHtml = new StringBuilder();
            var sbLinked = new StringBuilder();
            var sbCurrent = sbHtml;
            var sbKey = new StringBuilder();

            var tags = new List<Tag>();
            var onClosing = new Stack<string>();
            int keyLevel = -1;

            int linkedLevel = -1;
            string linkedKey = "";
            StringBuilder linkedTitle = new StringBuilder();
            int linkedTitleLevel = -1;

            var ClosingActions = new Dictionary<int,Action>();

            var st = new XmlReaderSettings();
            using (XmlReader reader = XmlReader.Create(new System.IO.StringReader(content)))
            {
                reader.Read();
                var firstKey = new Tag(reader);
                tags.Add(firstKey);
                onClosing.Push("");
                List<Tag> lastTag = null;

                if (firstKey.Attributes.ContainsKey("d:title"))
                {
                    sbKey.AppendLine("<key type=\"表記\">" + firstKey.Attributes["d:title"] + "</key>");
                }

                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.IsEmptyElement)
                            {
                                var tag = new Tag(reader);
                                switch (reader.Name.ToLower())
                                {
                                    case "br": sbCurrent.Append("<br />"); break;
                                    case "img": sbCurrent.Append("<a href=\"" + tag.Attributes["src"] + "\"></a>"); break;
                                }
                                lastTag = new List<Tag>(tags);
                            }
                            else
                            {
                                var tag = new Tag(reader);
                                tags.Add(tag);

                                var dec = FindMatchingDeclartion(tags, lastTag, Style.StyleRules);
                                var dec2 = FindMatchingDeclartion(tags, lastTag, StyleDic.StyleRules);
                                string closingString = "";

                                if (dec != null)
                                {
                                    {
                                        if (FindProperty(dec, "display")?.Term.ToString().ToLower() == "block")
                                        {
                                            sbCurrent.Append("<br />"); closingString = "<br />" + closingString;
                                        }
                                        else
                                        {
                                            {
                                                var property = FindProperty(dec, "margin-left");
                                                if (property != null)
                                                {
                                                    var match = Regex.Match(property.Term.ToString().ToLower(), @"([\d\-\+\.]+)[^\da-z]*?([a-z]*)");
                                                    double num;
                                                    if (!match.Success || !double.TryParse(match.Groups[1].Value, out num))
                                                    {
                                                        num = 0;
                                                    }
                                                    if (match.Groups[2].Value == "em")
                                                    {
                                                        sbCurrent.Append(new string(' ', (int)Math.Ceiling(Math.Max(num,0))));
                                                    }
                                                    else
                                                    {
                                                        sbCurrent.Append(" ");
                                                    }
                                                }
                                            }
                                            {
                                                var property = FindProperty(dec, "margin-right");
                                                if (property != null)
                                                {
                                                    var match = Regex.Match(property.Term.ToString().ToLower(), @"([\d\-\+\.]+)[^\da-z]*?([a-z]*)");
                                                    double num;
                                                    if (!match.Success || !double.TryParse(match.Groups[1].Value, out num))
                                                    {
                                                        num = 0;
                                                    }
                                                    if (match.Groups[2].Value == "em")
                                                    {
                                                        closingString = (new string(' ', (int)Math.Ceiling(Math.Max(num, 0)))) + closingString;
                                                    }
                                                    else
                                                    {
                                                        closingString = " " + closingString;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (keyLevel == tags.Count)
                                    {
                                        sbKey.AppendLine("</key>");
                                    }

                                    {
                                        var property = FindProperty(dec, "font-weight");
                                        if (property!=null && property.Term.ToString().ToLower() != "normal" && !onClosing.Contains("</b>"))
                                        {
                                            sbCurrent.Append("<b>"); closingString = "</b>" + closingString;
                                        }
                                    }
                                    
                                    {
                                        if (FindProperty(dec, "font-style")?.Term.ToString().ToLower() == "italic" && !onClosing.Contains("</i>"))
                                        {
                                            sbCurrent.Append("<i>"); closingString = "</i>" + closingString;
                                        }
                                    }
                                    {
                                        if (FindProperty(dec, "vertical-align")?.Term.ToString().ToLower() == "sub")
                                        {
                                            sbCurrent.Append("<sub>"); closingString = "</sub>" + closingString;
                                        }
                                    }
                                    {
                                        if (FindProperty(dec, "vertical-align")?.Term.ToString().ToLower() == "super")
                                        {
                                            sbCurrent.Append("<sup>"); closingString = "</sup>" + closingString;
                                        }
                                    }
                                }
                                if (dec2 != null)
                                {
                                    {
                                        if (FindProperty(dec2, "headWordGroup")?.Term.ToString().ToLower() == "true")
                                        {
                                            if (firstKey.Attributes.ContainsKey("id"))
                                            {
                                                sbCurrent.Append("<dt noindex=\"true\" id=\""+firstKey.Attributes["id"]+"\">");
                                            }else
                                            {
                                                sbCurrent.Append("<dt>");
                                            }

                                            closingString = "</dt>\n<keys /><dd>" + closingString;
                                        }
                                    }
                                    {
                                        if (FindProperty(dec2, "dfn")?.Term.ToString().ToLower() == "true")
                                        {
                                            sbCurrent.Append("<dfn>");
                                            closingString = "</dfn>" + closingString;
                                        }
                                    }
                                    {
                                        var property = FindProperty(dec2, "key");
                                        if (property != null)
                                        {
                                            var addTarget = "<key type=\"" + property.Term.ToString() + "\">";
                                            {
                                                keyLevel = tags.Count - 1;
                                                ClosingActions.Add(tags.Count - 1, () => { sbKey.AppendLine("</key>"); keyLevel = -1; });
                                                sbKey.Append(addTarget);
                                            }
                                        }
                                    }
                                    {
                                        var property = FindProperty(dec2, "category");
                                        if (property != null)
                                        {
                                            var addTarget = "<key type=\"複合\" name=\"" + property.Term.ToString() + "\">";
                                            {
                                                keyLevel = tags.Count - 1;
                                                ClosingActions.Add(tags.Count - 1, () => { sbKey.AppendLine("</key>"); keyLevel = -1; });
                                                sbKey.Append(addTarget);
                                            }
                                        }
                                    }
                                    {
                                        if (FindProperty(dec2, "reference")?.Term.ToString().ToLower() == "true" && linkedLevel==-1)
                                        {
                                            linkedKey ="linked_"+ refCnt.ToString();
                                            sbCurrent = sbLinked;
                                            sbCurrent.Append("\n<X4081>1F03 1F02</X4081><a name=\"" + linkedKey + "\"></a>");
                                            //sbCurrent.Append("<a name=\"" + linkedKey + "\"></a>");
                                            linkedLevel = tags.Count - 1;

                                            ClosingActions.Add(tags.Count - 1, () =>
                                            {
                                                var linkedTitleString = linkedTitle.ToString().Trim();
                                                //sbCurrent.Replace("<referenceTitle />", linkedTitle.ToString());
                                                //sbCurrent.Append("</dd>\n");
                                                sbCurrent = sbHtml;
                                                if (linkedTitle.ToString() == "") linkedTitle.Append("参照");
                                                sbCurrent.Append("<a href=\"#" + linkedKey + "\">→</a>" + linkedTitle.ToString() + "<br />");
                                                linkedTitle.Clear();
                                                linkedTitleLevel = -1;
                                                linkedLevel = -1;
                                            });
                                            refCnt++;
                                        }
                                        if (FindProperty(dec2, "referenceTitle")?.Term.ToString().ToLower() == "true" && linkedLevel != -1)
                                        {
                                            linkedTitleLevel = tags.Count - 1;
                                            ClosingActions.Add(tags.Count - 1, () => { linkedTitleLevel = -1; });

                                        }
                                    }
                                    {
                                        if (FindProperty(dec2, "display")?.Term.ToString().ToLower() == "none")
                                        {
                                            var sbTemporary = sbCurrent;
                                            sbCurrent = new StringBuilder();
                                            ClosingActions.Add(tags.Count - 1, () => { sbCurrent = sbTemporary; });
                                        }

                                    }
                                }

                                if (tag.Attributes.ContainsKey("id"))
                                {
                                    sbCurrent.Append("<a name=\"" + tag.Attributes["id"] + "\"></a>");
                                }
                                switch (tag.Name.ToLower())
                                {
                                    case "a":
                                        if (tag.Attributes.ContainsKey("href"))
                                        {
                                            sbCurrent.Append("<a href=\"" + ConvertUrl(tag.Attributes["href"]) + "\">");
                                            closingString = tag.ToStringClose() + closingString;
                                        }
                                        else if (tag.Attributes.ContainsKey("name"))
                                        {
                                            sbCurrent.Append("<a name=\""+tag.Attributes["name"]+"\"></a>");
                                        }
                                        break;
                                    case "i":
                                    case "sub":
                                    case "sup":
                                    case "bold":
                                    case "ul":
                                    case "ol":
                                    case "li":
                                        sbCurrent.Append(tag.ToString()); closingString = tag.ToStringClose() + closingString; break;
                                    case "table":
                                    case "tr":
                                    case "td":
                                    case "caption":
                                        sbCurrent.Append(tag.ToStringNoAttribute()); closingString = tag.ToStringClose() + closingString; break;
                                    default:
                                        break;
                                }
                                onClosing.Push(closingString);
                            }
                            break;
                        case XmlNodeType.Text:
                            sbCurrent.Append(EscapeHtml(reader.Value));
                            if (keyLevel!=-1&& keyLevel < tags.Count)
                            {
                                //var sjis = Encoding.GetEncoding(932, new EncoderReplacementFallback(""), new DecoderReplacementFallback(""));
                                //sbKey.Append(sjis.GetString(sjis.GetBytes(reader.Value)));
                                sbKey.Append(EscapeHtml(reader.Value)); 
                            }
                            if (linkedTitleLevel != -1 && linkedTitleLevel < tags.Count)
                            {
                                linkedTitle.Append(EscapeHtml(reader.Value)+" ");
                            }
                            break;
                        case XmlNodeType.Comment:
                            break;
                        case XmlNodeType.EndElement:
                            sbCurrent.Append(onClosing.Pop());
                            lastTag = new List<Tag>(tags);
                            tags.RemoveAt(tags.Count - 1);

                            if (ClosingActions.ContainsKey(tags.Count))
                            {
                                ClosingActions[tags.Count]();
                                ClosingActions.Remove(tags.Count);
                            }
                            break;
                    }
                }
            }
            sbHtml.Append("</dd>\n");
            sbHtml.AppendLine();
            sbHtml.Replace("<keys />", sbKey.ToString());

            linked = DeleteDoubleBreak(sbLinked.ToString());
            html= DeleteDoubleBreak(sbHtml.ToString());
        }

        static string EscapeHtml(string src)
        {
            return src.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        static string AddTextIfNotAlreadyExist(string target,string text)
        {
            if (target.Contains(text)) { return target; }
            return target + text;
        }

        static string ReplaceRepeatRegex(string org, string from, string to)
        {
            var reg = new Regex(from);
            while (reg.Match(org).Success)
            {
                org = reg.Replace(org, to);
            }
            return org;
        }

        static string DeleteDoubleBreak(string arg)
        {
            arg = ReplaceRepeatRegex(arg, @"(<br />)+((<[^<>]+>)*?\n?<X4081>)", "$2");
            arg = ReplaceRepeatRegex(arg, @"(<\/X4081>(<[^<>]+>)*?)(<br />)+", "$1");
            arg = ReplaceRepeatRegex(arg, @"(<br />)+((<[^<>]+>)*?<\/d[td]>)", "$2");
            arg = ReplaceRepeatRegex(arg, @"(<d[td]>(<[^<>]+>)*?)(<br />)+", "$1");
            arg = ReplaceRepeatRegex(arg, "(<br />){2,}", "<br />");
            arg = ReplaceRepeatRegex(arg, "(<br />)+((<[^<>]+>)+)(<br />)+", "<br />$2");
            return arg;
        }

        static string DeleteTripleBreak(string arg)
        {
            arg = ReplaceRepeatRegex(arg, @"(<br />)+((<[^<>]+>)*?<\/d[td]>)", "$2");
            arg = ReplaceRepeatRegex(arg, @"(<d[td]>(<[^<>]+>)*?)(<br />)+", "$1");
            arg = ReplaceRepeatRegex(arg, "(<br />){3,}", "<br /><br />");
            arg = ReplaceRepeatRegex(arg, "(<br />)+((<[^<>]+>)+)(<br />)+", "<br />$2<br />");
            return arg;
        }

        static string ReplaceRepeat(string org,string from,string to)
        {
            while (org.Contains(from))
            {
                org = org.Replace(from, to);
            }
            return org;
        }

        static string ConvertUrl(string arg)
        {
            {
                var regex = new Regex("^[^\\\"]+?#xpointer\\(\\/\\/\\*\\[\\@id\\=\\'([^\']+)\\'\\]\\)$");
                var match = regex.Match(arg);
                if (match.Success) { return "#"+match.Groups[1].Value; }
            }
            {
                var regex = new Regex("^x-dictionary\\:r\\:([^\\\"\\:]+?)\\:[^\\\"]*$");
                var match = regex.Match(arg);
                if (match.Success) { return "#" + match.Groups[1].Value; }
            }
            return arg;
        }

        static ExCSS.Property FindProperty(List<ExCSS.StyleRule> rules,string Name)
        {
            ExCSS.Property lastProperty = null;
            foreach (var dec in rules)
            {
                foreach (var b in dec.Declarations)
                {
                    if (b.Name.ToLower() == Name.ToLower())
                    {
                        lastProperty= b;
                    }
                }
            }
            return lastProperty;
        }

        static List<ExCSS.StyleRule> FindMatchingDeclartion(List<Tag> tags, List<Tag> lastTag, IList<ExCSS.StyleRule> sr)
        {
            var result = new List<ExCSS.StyleRule>();
            foreach (var sel in sr)
            {
                if (MatchSelector(tags,lastTag, sel.Selector)==tags.Count-1) {
                    result.Add(sel);
                }
            }
            return result;
        }

        static int MatchSelector(List<Tag> tags, List<Tag> lastTag, ExCSS.BaseSelector sel, int startCount=0)
        {
            if (sel is ExCSS.SimpleSelector)
            {
                var s = (sel as ExCSS.SimpleSelector);
                for(int i = startCount; i < tags.Count; i++)
                {
                    var currentTag = tags[i];
                    var sStr = s.ToString();
                    var classes = currentTag.Attributes.Keys.Contains("class") ? currentTag.Attributes["class"].Split(' ') : new string[0];

                    if (classes.Count() > 1)
                    {
                        var tn = "." + string.Join(".", classes);
                        if (sStr == tn || sStr == currentTag.Name + tn)
                        {
                            return i;
                        }
                    }
                    foreach (var className in classes)
                    {
                        if (sStr == "*" || sStr == currentTag.Name || sStr == "." + className || sStr == "*." + className || sStr == currentTag.Name + "." + className)
                        {
                            return i;
                        }
                    }
                    if(sStr.First()=='['&& sStr.Last() == ']')
                    {
                        var g=sStr.Substring(1, sStr.Length - 2);
                        var gs=g.Split(new char[] { '=' }, 2);
                        if(currentTag.Attributes.ContainsKey(gs[0])&& currentTag.Attributes[gs[0]] == "\""+gs[1]+ "\"")
                        {
                            return i;
                        }
                    }
                }
                return -1;
            } else if (sel is ExCSS.ComplexSelector)
            {
                var s = (sel as ExCSS.ComplexSelector);
                int i = startCount;
                ExCSS.Combinator comb = ExCSS.Combinator.Descendent;

                int adjCnt = 0;
                foreach (var c in s)
                {
                    if (comb == ExCSS.Combinator.AdjacentSibling) adjCnt++;
                }
                if (adjCnt >= 2) return -1;
                foreach (var c in s)
                {
                    if (i >= tags.Count) { return -1; }
                    var tmp = adjCnt==0 ? MatchSelector(tags, lastTag, c.Selector, i) : MatchSelector(lastTag,null, c.Selector, i);
                    if (tmp == -1) { return -1; }
                    if (comb == ExCSS.Combinator.Child&& tmp != i)
                    {
                        return MatchSelector(tags, lastTag, sel, i);
                    }
                    else if(comb ==ExCSS.Combinator.AdjacentSibling)
                    {
                        if (i != tags.Count - 1) { return -1; }
                        //i = startCount;
                        adjCnt--;
                    }
                    i = tmp + 1;

                    comb = c.Delimiter;
                }
                return i - 1;
            }
            else if(sel is ExCSS.SelectorList)
            {
                var s = sel as ExCSS.SelectorList;
                int finalResult = -1;
                foreach (var c in s)
                {
                    int result;
                    if (c is ExCSS.SimpleSelector && c.ToString()[0] == '[')
                    {
                        if ((result = MatchSelector(tags, lastTag, c, startCount)) != finalResult)
                        {
                            return -1;
                        }
                    }
                    else
                    {
                        if ((result = MatchSelector(tags, lastTag, c, startCount)) != -1)
                        {
                            finalResult = Math.Max(result, finalResult);
                        }
                    }
                }
                return finalResult;
            }
            else
            {
                return -1;
                //throw new NotImplementedException();
            }
        }

        public class Tag
        {
            public string Name;
            public Dictionary<string, string> Attributes = new Dictionary<string, string>();

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("<" + this.Name);
                foreach(var attr in Attributes)
                {
                    sb.Append(" " + attr.Key + "=\"" + attr.Value + "\"");
                }
                sb.Append(">");
                return sb.ToString();
            }

            public string ToStringNoAttribute()
            {
                return "<" + this.Name + ">"; ;
            }

            public string ToStringClose()
            {
                return "</" + this.Name + ">";
            }

            public Tag(XmlReader reader)
            {
                this.Name = reader.Name;
                if (reader.HasAttributes)
                {
                    while (reader.MoveToNextAttribute())
                    {
                        Attributes[reader.Name] = reader.Value;
                    }
                    reader.MoveToElement();
                }
            }
        }

    }
}
