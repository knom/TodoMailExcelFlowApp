using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Knom.LogicApps.ExtractMailBody.Controllers
{
    [Route("api/[controller]")]
    public class HtmlPlainTextController : Controller
    {
        [HttpPost]
        public string Post(bool multiLine)
        {
            string body = GetBody(this.Request);

            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(body);

            string result = ToPlainText(document);

            if (multiLine)
            {
                return result;
            }
            else
            {
                return result.Substring(0, result.IndexOf("\r\n", StringComparison.Ordinal));
            }
        }
        private static string GetBody(HttpRequest request)
        {
            using (var receiveStream = request.Body)
            {
                using (var readStream = new StreamReader(receiveStream, Encoding.UTF8))
                {
                    return readStream.ReadToEnd();
                }
            }
        }

        // From here on..
        // From http://stackoverflow.com/questions/29995333/convert-render-html-to-text-with-correct-line-breaks
        // By http://stackoverflow.com/users/1785164/serge-shultz

        private static string ToPlainText(HtmlDocument doc)
        {
            var builder = new StringBuilder();
            var state = ToPlainTextState.StartLine;

            Plain(builder, ref state, new[] { doc.DocumentNode });
            return builder.ToString();
        }
        private static void Plain(StringBuilder builder, ref ToPlainTextState state, IEnumerable<HtmlAgilityPack.HtmlNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node is HtmlTextNode)
                {
                    var text = (HtmlTextNode)node;
                    Process(builder, ref state, HtmlEntity.DeEntitize(text.Text).ToCharArray());
                }
                else
                {
                    var tag = node.Name.ToLower();

                    if (tag == "br")
                    {
                        builder.AppendLine();
                        state = ToPlainTextState.StartLine;
                    }
                    else if (NonVisibleTags.Contains(tag))
                    {
                    }
                    else if (InlineTags.Contains(tag))
                    {
                        Plain(builder, ref state, node.ChildNodes);
                    }
                    else
                    {
                        if (state != ToPlainTextState.StartLine)
                        {
                            builder.AppendLine();
                            state = ToPlainTextState.StartLine;
                        }
                        Plain(builder, ref state, node.ChildNodes);
                        if (state != ToPlainTextState.StartLine)
                        {
                            builder.AppendLine();
                            state = ToPlainTextState.StartLine;
                        }
                    }

                }

            }
        }

        //System.Xml.Linq part
        public static string ToPlainText(IEnumerable<XNode> nodes)
        {
            var builder = new StringBuilder();
            var state = ToPlainTextState.StartLine;

            Plain(builder, ref state, nodes);
            return builder.ToString();
        }
        static void Plain(StringBuilder builder, ref ToPlainTextState state, IEnumerable<XNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node is XElement)
                {
                    var element = (XElement)node;
                    var tag = element.Name.LocalName.ToLower();

                    if (tag == "br")
                    {
                        builder.AppendLine();
                        state = ToPlainTextState.StartLine;
                    }
                    else if (NonVisibleTags.Contains(tag))
                    {
                    }
                    else if (InlineTags.Contains(tag))
                    {
                        Plain(builder, ref state, element.Nodes());
                    }
                    else
                    {
                        if (state != ToPlainTextState.StartLine)
                        {
                            builder.AppendLine();
                            state = ToPlainTextState.StartLine;
                        }
                        Plain(builder, ref state, element.Nodes());
                        if (state != ToPlainTextState.StartLine)
                        {
                            builder.AppendLine();
                            state = ToPlainTextState.StartLine;
                        }
                    }

                }
                else if (node is XText)
                {
                    var text = (XText)node;
                    Process(builder, ref state, text.Value.ToCharArray());
                }
            }
        }
        //common part
        public static void Process(StringBuilder builder, ref ToPlainTextState state, params char[] chars)
        {
            foreach (var ch in chars)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (IsHardSpace(ch))
                    {
                        if (state == ToPlainTextState.WhiteSpace)
                            builder.Append(' ');
                        builder.Append(' ');
                        state = ToPlainTextState.NotWhiteSpace;
                    }
                    else
                    {
                        if (state == ToPlainTextState.NotWhiteSpace)
                            state = ToPlainTextState.WhiteSpace;
                    }
                }
                else
                {
                    if (state == ToPlainTextState.WhiteSpace)
                        builder.Append(' ');
                    builder.Append(ch);
                    state = ToPlainTextState.NotWhiteSpace;
                }
            }
        }
        static bool IsHardSpace(char ch)
        {
            return ch == 0xA0 || ch == 0x2007 || ch == 0x202F;
        }

        private static readonly HashSet<string> InlineTags = new HashSet<string>
        {
            //from https://developer.mozilla.org/en-US/docs/Web/HTML/Inline_elemente
            "b", "big", "i", "small", "tt", "abbr", "acronym",
            "cite", "code", "dfn", "em", "kbd", "strong", "samp",
            "var", "a", "bdo", "br", "img", "map", "object", "q",
            "script", "span", "sub", "sup", "button", "input", "label",
            "select", "textarea"
        };

        private static readonly HashSet<string> NonVisibleTags = new HashSet<string>
        {
            "script", "style"
        };

        public enum ToPlainTextState
        {
            StartLine = 0,
            NotWhiteSpace,
            WhiteSpace,
        }

    }
}
