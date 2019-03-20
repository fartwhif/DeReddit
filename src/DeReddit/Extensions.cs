using Humanizer;
using RedditSharp.Things;
using System;

namespace dereddit
{
    internal static class Extensions
    {
        public static string AsString(this Comment cmt)
        {
            return $"{cmt.Permalink} {cmt.AuthorName} {"points".ToQuantity(cmt.Score)} {(DateTime.Now - cmt.Created).Humanize()} ago  |  {cmt.Body.Replace("<br>", "<~fakebr~>").Replace("\r\n", "\n").Replace("\n", "<br>")}";
        }
        public static string AsString(this Post pst)
        {
            return $"{pst.Permalink} {pst.AuthorName} {"points".ToQuantity(pst.Score)} {(DateTime.Now - pst.Created).Humanize()} ago  |  {pst.Url}  |  {pst.Title.Replace("<br>", "<~fakebr~>").Replace("\r\n", "\n").Replace("\n", "<br>")} {((pst.SelfText == null) ? "" : pst.SelfText.Replace("<br>", "<~fakebr~>").Replace("\r\n", "\n").Replace("\n", "<br>"))}";
        }
    }
}
