using Microsoft.Extensions.Configuration;
using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS0168 
namespace dereddit
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {

                Program g = new Program();
                Task startTask = g.Go();
                startTask.Wait();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("401 (Unauthorized)"))
                {
                    Console.WriteLine("before running this the configuration must be set, open and edit the file appsettings.json and try again");
                }
                else
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }

        private IConfigurationRoot Config = null;
        private async Task Go()
        {
            Config = Cfg.Config;

            bool d1 = bool.Parse(Config["Delete"]);
            bool o1 = bool.Parse(Config["Overwrite"]);
            if (d1 && !o1)
            {
                Console.WriteLine("Fix your configuration file.  If you delete without also overwriting, then the original comment will remain in the reddit database even though the comment is deleted, according to Reddit admins.");
                return;
            }
            rand = new Random(BitConverter.ToInt32(new Guid().ToByteArray().Take(4).ToArray()));
            AuthenticatedFixture authFixture = new AuthenticatedFixture();
            Reddit reddit = new Reddit(authFixture.WebAgent, true);

            IEnumerable<string> srs = Config.GetSection("Places:SubReddits").GetChildren().ToList().Select(x => x.Value);
            foreach (string srn in srs)
            {
                Subreddit sr = await reddit.GetSubredditAsync(srn, true);

                //by comment
                Console.WriteLine($"comments from {srn} follow");
                List<InterestingComment> interstingComments = new List<InterestingComment>();
                int junk = 0;
                IAsyncEnumerator<Comment> comments = sr.GetComments().GetEnumerator(50, -1, false);
                while (await comments.MoveNext(CancellationToken.None))
                {
                    Comment comment = comments.Current;

                    if (comment.Vote == VotableThing.VoteType.Upvote && bool.Parse(Config["UnUpVote"]))
                    {
                        try
                        {
                            await comment.ClearVote();
                            InterestingLog("un-up-vote +1 => 0  |  " + comment.AsString());
                        }
                        catch (Exception ex)
                        {
                            //can't check for archived without walking up the heirarchy...
                            //InterestingLog($"failed to un-up-vote because {ex.Message}  |  {comment.AsString()}");
                        }
                    }
                    else if (comment.Vote == VotableThing.VoteType.Downvote && bool.Parse(Config["UnDownVote"]))
                    {
                        try
                        {
                            await comment.ClearVote();
                            InterestingLog("un-down-vote -1 => 0  |  " + comment.AsString());
                        }
                        catch (Exception ex)
                        {
                            //can't check for archived without walking up the heirarchy...
                            //InterestingLog($"failed to un-down-vote because {ex.Message}  |  {comment.AsString()}");
                        }
                    }

                    if (IsInteresting(comment))
                    {
                        
                        if (UserIsAuthor(comment))
                        {
                            try
                            {
                                InterestingComment inter = new InterestingComment() { Comment = comment };
                                InterestingLog(inter);
                                interstingComments.Add(inter);
                                if (bool.Parse(Config["Overwrite"]))
                                {
                                    await comment.EditTextAsync(Dust());
                                    await Task.Delay(1000); // just in case...
                                    inter.Overwritten = true;
                                    InterestingLog(inter);
                                }
                                if (bool.Parse(Config["Delete"]))
                                {
                                    await comment.DelAsync();
                                    inter.Deleted = true;
                                    await Task.Delay(1000); // just in case...
                                    InterestingLog(inter);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debugger.Break();
                            }
                        }
                        else
                        {
                            InterestingLog(new InterestingComment() { Comment = comment });
                        }
                    }
                    else
                    {
                        JunkLog(new JunkComment() { Comment = comment });
                        junk++;
                    }
                }

                Console.WriteLine($"done with {srn} comments, interesting: {interstingComments.Count}, junk: {junk}");
                if (interstingComments.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Interesting comments from {srn} follow:");
                    foreach (InterestingComment inter in interstingComments)
                    {
                        Console.WriteLine(inter);
                    }
                    Console.WriteLine();
                }

                //by post
                Console.WriteLine($"posts from {srn} follow");
                List<InterestingPost> interstingPosts = new List<InterestingPost>();
                junk = 0;

                IAsyncEnumerator<Post> posts = sr.GetPosts().GetEnumerator(50, -1, false);
                while (await posts.MoveNext(CancellationToken.None))
                {
                    Post post = posts.Current;

                    if (IsInteresting(post))
                    {
                        if (UserIsAuthor(post))
                        {
                            try
                            {
                                InterestingPost inter = new InterestingPost() { Post = post };
                                InterestingLog(inter);
                                interstingPosts.Add(inter);
                                if (bool.Parse(Config["Overwrite"]))
                                {
                                    await post.EditTextAsync(Dust());
                                    await Task.Delay(1000); // just in case...
                                    inter.Overwritten = true;
                                    InterestingLog(inter);
                                }
                                if (bool.Parse(Config["Delete"]))
                                {
                                    await post.DelAsync();
                                    inter.Deleted = true;
                                    await Task.Delay(1000); // just in case...
                                    InterestingLog(inter);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debugger.Break();
                            }
                        }
                        else
                        {
                            InterestingLog(new InterestingPost() { Post = post });
                        }
                    }
                    else
                    {
                        JunkLog(new JunkPost() { Post = post });
                        junk++;
                    }
                }

                Console.WriteLine($"done with {srn} posts, interesting: {interstingPosts.Count}, junk: {junk}");
                if (interstingPosts.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Interesting posts from {srn} follow:");
                    foreach (InterestingPost inter in interstingPosts)
                    {
                        Console.WriteLine(inter);
                    }
                    Console.WriteLine();
                }
            }
            if (bool.Parse(Config["UnDownVote"]) || bool.Parse(Config["UnUpVote"]))
            {
                RedditUser user = await RedditUser.GetUserAsync(authFixture.WebAgent, authFixture.UserName);
                if (bool.Parse(Config["UnDownVote"]))
                {
                    IAsyncEnumerator<Post> enumDisliked = user.GetDislikedPosts().GetEnumerator();
                    while (await enumDisliked.MoveNext(CancellationToken.None))
                    {
                        Post disliked = enumDisliked.Current;
                        if (!disliked.IsArchived)
                        {
                            try
                            {
                                await disliked.SetVoteAsync(VotableThing.VoteType.None);
                                InterestingLog("un-down-vote -1 => 0  |  " + disliked.AsString());
                            }
                            catch (Exception ex)
                            {
                                InterestingLog($"failed to un-down-vote because {ex.Message}  |  {disliked.AsString()}");
                            }
                        }
                    }
                }
                if (bool.Parse(Config["UnUpVote"]))
                {
                    IAsyncEnumerator<Post> enumLiked = user.GetLikedPosts().GetEnumerator();
                    while (await enumLiked.MoveNext(CancellationToken.None))
                    {
                        Post liked = enumLiked.Current;
                        if (!liked.IsArchived)
                        {
                            try
                            {
                                await liked.SetVoteAsync(VotableThing.VoteType.None);
                            }
                            catch (Exception ex)
                            {
                                InterestingLog($"failed to un-up-vote because {ex.Message}  |  {liked.AsString()}");
                            }
                            InterestingLog("un-up-vote +1 => 0  |  " + liked.AsString());
                        }
                    }
                }
            }
        }

        private class JunkPost
        {
            public Post Post { get; set; }
            public override string ToString()
            {
                return Post.AsString();
            }
        }
        private class InterestingPost
        {
            public Post Post { get; set; }
            public bool Overwritten { get; set; }
            public bool Deleted { get; set; }
            public override string ToString()
            {
                return $"Overwritten: {Overwritten}, Deleted: {Deleted}, {Post.AsString()}";
            }
        }
        private class JunkComment
        {
            public Comment Comment { get; set; }
            public override string ToString()
            {
                return Comment.AsString();
            }
        }
        private class InterestingComment
        {
            public Comment Comment { get; set; }
            public bool Overwritten { get; set; }
            public bool Deleted { get; set; }
            public override string ToString()
            {
                return $"Overwritten: {Overwritten}, Deleted: {Deleted}, {Comment.AsString()}";
            }
        }
        private void InterestingLog(InterestingPost pst)
        {
            InterestingLog(pst.ToString());
        }
        private void InterestingLog(string inter)
        {
            inter = inter + (inter.EndsWith(Environment.NewLine) ? "" : Environment.NewLine);
            if (bool.Parse(Config["InterestingLog"]))
            {
                File.AppendAllText(FpInterestingLog, inter);
            }
            Console.Write($"INTERESTING: {inter}");
        }
        private void JunkLog(string jnk)
        {
            jnk = jnk + (jnk.EndsWith(Environment.NewLine) ? "" : Environment.NewLine);
            if (bool.Parse(Config["JunkLog"]))
            {
                File.AppendAllText(FpJunkLog, jnk);
            }
            Console.Write($"JUNK: {jnk}");
        }
        private void JunkLog(JunkPost pst)
        {
            JunkLog(pst.ToString());
        }
        private void InterestingLog(InterestingComment cmt)
        {
            InterestingLog(cmt.ToString());
        }
        private void JunkLog(JunkComment cmt)
        {
            JunkLog(cmt.ToString());
        }
        private string FpInterestingLog => Path.Combine(Environment.CurrentDirectory, "interesting.txt");
        private string FpJunkLog => Path.Combine(Environment.CurrentDirectory, "junk.txt");
        /// <summary>
        /// generates some dust
        /// thanks to somenewguy for the pattern at: 
        /// https://greasyfork.org/en/scripts/10380-reddit-overwrite
        /// </summary>
        /// <returns>database dust</returns>
        private string Dust()
        {
            string mote1 = "";
            string mote2 = "";
            for (; ; )
            {
                mote1 = "^^^^^^^^^^^^^^^^" + rand.NextDouble().ToString();
                if (mote1.Length > 22)
                {
                    mote1 = mote1.Substring(0, 22);
                    break;
                }
            }
            for (; ; )
            {
                mote2 = rand.NextDouble().ToString();
                if (mote2.Length > 7)
                {
                    mote2 = mote2.Substring(2, 5);
                    break;
                }
            }
            return "deleted  " + mote1 + "  [^^^What ^^^is ^^^this?](https://pastebin.com/yBuKRiTW/" + mote2 + ")";
        }
        private Random rand = null;
        private bool UserIsAuthor(Comment cmt)
        {
            if (cmt == null || cmt.AuthorName == null)
            {
                return false;
            }
            return cmt.AuthorName.ToLower().Trim() == Needle;
        }
        private bool UserIsAuthor(Post pst)
        {
            if (pst == null || pst.AuthorName == null)
            {
                return false;
            }
            return pst.AuthorName.ToLower().Trim() == Needle;
        }
        private bool IsInteresting(Post pst)
        {
            return IsInteresting(pst.AuthorName ?? null) || IsInteresting(pst.Title ?? null) || IsInteresting(pst.SelfText ?? null) || IsInteresting(pst.LinkFlairText ?? null) || IsInteresting(pst.Url ?? null) || IsInteresting(pst.RawJson?.ToString());
        }
        private bool IsInteresting(Comment cmt)
        {
            return IsInteresting(cmt.AuthorName ?? null) || IsInteresting(cmt.Body ?? null) || IsInteresting(cmt.RawJson?.ToString()); ;
        }
        private bool IsInteresting(Uri hayBail)
        {
            if (hayBail == null)
            {
                return false;
            }
            return IsInteresting(hayBail.ToString());
        }
        private bool IsInteresting(string hayBail)
        {
            if (hayBail == null)
            {
                return false;
            }
            if (hayBail.ToLower().Contains(Needle))
            {
                return true;
            }
            return false;
        }
        private string Needle => Config["UserName"].ToLower().Trim();
    }
    internal static class Cfg
    {
        public static IConfigurationRoot Config
        {
            get
            {
                IConfigurationBuilder builder = new ConfigurationBuilder()
                    .SetBasePath(Environment.CurrentDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                //.AddEnvironmentVariables();
                return builder.Build();
            }
        }
    }
    internal class AuthenticatedFixture
    {
        public string AccessToken { get; private set; }
        public BotWebAgent WebAgent { get; set; }
        public string UserName { get; private set; }
        public AuthenticatedFixture()
        {
            IConfigurationRoot Config = Cfg.Config;
            WebAgent = new BotWebAgent(Config["UserName"], Config["UserPassword"],
                Config["RedditClientID"], Config["RedditClientSecret"], Config["RedditRedirectURI"]);
            AccessToken = WebAgent.AccessToken;
            UserName = Config["UserName"];
        }
    }
}


