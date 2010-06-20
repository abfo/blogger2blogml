using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using BlogML;
using BlogML.Xml;

namespace Blogger2BlogML
{
    /// <summary>
    /// Converts a blogger ATOM export file to a BlogML file
    /// </summary>
    public class BlogConverter
    {
        private class CommentWithInReplyTo
        {
            public BlogMLComment Comment { get; private set; }
            public string InReplyTo { get; private set; }

            public CommentWithInReplyTo(BlogMLComment comment, string inReplyTo)
            {
                this.Comment = comment;
                this.InReplyTo = inReplyTo;
            }
        }

        private static readonly XNamespace AtomNamespace = "http://www.w3.org/2005/Atom";
        private static readonly XNamespace ThrNamespace = "http://purl.org/syndication/thread/1.0";

        private string _bloggerExportPath;
        private string _blogmlPath;

        /// <summary>
        /// Reports messsages to display or log during conversion
        /// </summary>
        public event EventHandler<ConverterMessageEventArgs> ConverterMessage;

        /// <summary>
        /// Converts a blogger ATOM export file to a BlogML file
        /// </summary>
        /// <param name="bloggerExportPath">Path to the blogger export file - must exist</param>
        /// <param name="blogmlPath">Path to the create the blogml file, will be overwritten if it alreay exists</param>
        public BlogConverter(string bloggerExportPath, string blogmlPath)
        {
            if (bloggerExportPath == null) { throw new ArgumentNullException("bloggerExportPath", Properties.Resources.Converter_BloggerExportNull); }
            if (blogmlPath == null) { throw new ArgumentNullException("blogmlPath", Properties.Resources.Converter_BlogMLPathNull); }
            if (!File.Exists(bloggerExportPath)) { throw new FileNotFoundException(Properties.Resources.Converter_BloggerExportMissing, bloggerExportPath); }
            if (!Directory.Exists(Path.GetDirectoryName(blogmlPath))) { throw new DirectoryNotFoundException(Properties.Resources.Converter_BlogMLPathMissing);}

            _bloggerExportPath = bloggerExportPath;
            _blogmlPath = blogmlPath;
        }

        /// <summary>
        /// Converts the blog export to BlogML
        /// </summary>
        public void Convert()
        {
            XDocument blogger = XDocument.Load(_bloggerExportPath);

            // extract basic information
            string blogTitle = blogger.Element(AtomNamespace + "feed").Element(AtomNamespace + "title").Value;
            DateTime blogUpdated = DateTime.Parse(blogger.Element(AtomNamespace + "feed").Element(AtomNamespace + "updated").Value).ToUniversalTime();
            string authorName = blogger.Element(AtomNamespace + "feed").Element(AtomNamespace + "author").Element(AtomNamespace + "name").Value;
            string authorUri = blogger.Element(AtomNamespace + "feed").Element(AtomNamespace + "author").Element(AtomNamespace + "uri").Value;
            string authorEmail = blogger.Element(AtomNamespace + "feed").Element(AtomNamespace + "author").Element(AtomNamespace + "email").Value;

            // assume the updated date and then hunt backwards in time
            DateTime blogCreated = blogUpdated;

            LogMessage(string.Format(CultureInfo.CurrentCulture,
                Properties.Resources.Converter_MessageBlogTitle,
                blogTitle,
                blogUpdated));

            LogMessage(string.Format(CultureInfo.CurrentCulture,
                Properties.Resources.Converter_MessageBlogAuthor,
                authorName,
                authorEmail,
                authorUri));

            // parse ATOM entries
            var query = from entry in blogger.Descendants(AtomNamespace + "entry")
                        select new
                        {
                            Id = entry.Element(AtomNamespace + "id").Value,
                            Published  = DateTime.Parse(entry.Element(AtomNamespace + "published").Value).ToUniversalTime(),
                            Updated = DateTime.Parse(entry.Element(AtomNamespace + "updated").Value).ToUniversalTime(),
                            Title = entry.Element(AtomNamespace + "title").Value,
                            Content = entry.Element(AtomNamespace + "content").Value,
                            AuthorName = entry.Element(AtomNamespace + "author").Element(AtomNamespace + "name").Value,
                            AuthorUri = entry.Element(AtomNamespace + "author").Element(AtomNamespace + "uri") == null ? null : entry.Element(AtomNamespace + "author").Element(AtomNamespace + "uri").Value,
                            AuthorEmail = entry.Element(AtomNamespace + "author").Element(AtomNamespace + "email") == null ? null : entry.Element(AtomNamespace + "author").Element(AtomNamespace + "email").Value,
                            InReplyTo = entry.Element(ThrNamespace + "in-reply-to") == null ? null : entry.Element(ThrNamespace + "in-reply-to").Attribute("ref").Value,
                            Categories = (from category in entry.Descendants(AtomNamespace + "category")
                                          select new
                                          {
                                              Scheme = category.Attribute("scheme").Value,
                                              Term = category.Attribute("term").Value,
                                          }),
                            Links = (from link in entry.Descendants(AtomNamespace + "link")
                                     select new
                                     {
                                         Rel = link.Attribute("rel").Value,
                                         Type = link.Attribute("type").Value,
                                         Href = link.Attribute("href").Value,
                                     }),
                        };

            // separate out the different export categories from the ATOM entries
            Dictionary<string, BlogMLPost> posts = new Dictionary<string, BlogMLPost>();
            List<CommentWithInReplyTo> comments = new List<CommentWithInReplyTo>();
            List<BlogMLAuthor> authors = new List<BlogMLAuthor>();
            List<BlogMLCategory> categories = new List<BlogMLCategory>();
            NameValueCollection settings = new NameValueCollection();
            string template = null;

            foreach (var q in query)
            {
                // update the blog created date as we find earlier entires
                if (q.Published < blogCreated)
                {
                    blogCreated = q.Published;
                }

                // create a content holder
                BlogMLContent content = new BlogMLContent();
                content.Text = q.Content;

                // find categories and the type of entry
                List<BlogMLCategoryReference> categoryRefs = new List<BlogMLCategoryReference>();
                string entryKind = null;

                // get the type of entry and any post categories
                foreach (var c in q.Categories)
                {
                    if (c.Scheme == "http://schemas.google.com/g/2005#kind")
                    {
                        entryKind = c.Term;
                    }
                    else if (c.Scheme == "http://www.blogger.com/atom/ns#")
                    {
                        BlogMLCategoryReference categoryRef = new BlogMLCategoryReference();
                        categoryRef.Ref = UpdateCategoriesGetRef(ref categories, c.Term, q.Published);
                        categoryRefs.Add(categoryRef);
                    }
                    else
                    {
                        // we've found a category scheme we don't know about
                        LogMessage(string.Format(CultureInfo.CurrentCulture, 
                            Properties.Resources.Converter_MessageUnexpectedCategoryScheme, 
                            c.Scheme));
                    }
                }

                // process entry based on the entry kind
                switch (entryKind)
                {
                    case "http://schemas.google.com/blogger/2008/kind#template":
                        template = q.Content;
                        break;

                    case "http://schemas.google.com/blogger/2008/kind#settings":
                        LogMessage(string.Format(CultureInfo.CurrentCulture,
                            Properties.Resources.Converter_ImportingSettings,
                            q.Id,
                            q.Content));

                        settings.Add(q.Id, q.Content);
                        break;

                    case "http://schemas.google.com/blogger/2008/kind#post":
                        LogMessage(string.Format(CultureInfo.CurrentCulture,
                            Properties.Resources.Converter_ImportingPost,
                            q.Title));

                        // get a reference to the author of this entry
                        BlogMLAuthorReference authorReference = new BlogMLAuthorReference();
                        authorReference.Ref = UpdateAuthorsGetRef(ref authors, q.AuthorName, q.AuthorEmail, q.Published);

                        BlogMLPost post = new BlogMLPost();
                        post.Approved = true;
                        post.Authors.Add(authorReference);
                        if (categoryRefs.Count > 0)
                        {
                            post.Categories.AddRange(categoryRefs);
                        }
                        post.Content = content;
                        post.DateCreated = q.Published;
                        post.DateModified = q.Updated;
                        post.HasExcerpt = false;
                        post.ID = q.Id;
                        post.PostType = BlogPostTypes.Normal;
                        post.Title = q.Title;

                        posts.Add(q.Id, post);
                        break;

                    case "http://schemas.google.com/blogger/2008/kind#comment":
                        LogMessage(string.Format(CultureInfo.CurrentCulture,
                            Properties.Resources.Converter_ImportingComment,
                            q.Title));

                        BlogMLComment comment = new BlogMLComment();
                        comment.Approved = true;
                        comment.Content = content;
                        comment.DateCreated = q.Published;
                        comment.DateModified = q.Updated;
                        comment.ID = q.Id;
                        comment.Title = q.Title;
                        comment.UserEMail = q.AuthorEmail;
                        comment.UserName = q.AuthorName;
                        comment.UserUrl = q.AuthorUri;

                        comments.Add(new CommentWithInReplyTo(comment, q.InReplyTo));
                        break;

                    default:
                        LogMessage(string.Format(CultureInfo.CurrentCulture,
                            Properties.Resources.Converter_MessageUnexpectedEntryKind,
                            entryKind));
                        break;
                }
            }

            // add comments to posts
            foreach (CommentWithInReplyTo comment in comments)
            {
                if (posts.ContainsKey(comment.InReplyTo))
                {
                    BlogMLPost post = posts[comment.InReplyTo];

                    LogMessage(string.Format(CultureInfo.CurrentCulture,
                        Properties.Resources.Converter_AttachingComment,
                        comment.Comment.Title,
                        post.Title));

                    post.Comments.Add(comment.Comment);
                }
                else
                {
                    LogMessage(string.Format(CultureInfo.CurrentCulture,
                        Properties.Resources.Converter_OrphanedComment,
                        comment.Comment.Title));
                }
            }

            // build the blog
            LogMessage(Properties.Resources.Converter_BuildingBlogML);

            BlogMLBlog blog = new BlogMLBlog();
            blog.Authors.AddRange(authors);
            blog.Categories.AddRange(categories);
            blog.DateCreated = blogCreated;
            blog.Posts.AddRange(posts.Values);
            blog.Title = blogTitle;
            
            // add blogger settings as extended properties
            foreach (string name in settings.Keys)
            {
                Pair<string, string> pair = new Pair<string, string>();
                pair.Key = name;
                pair.Value = settings[name];
                blog.ExtendedProperties.Add(pair);
            }

            // output BlogML
            LogMessage(string.Format(CultureInfo.CurrentCulture,
                Properties.Resources.Converter_WritingBlogML,
                _blogmlPath));

            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.CheckCharacters = true;
            writerSettings.CloseOutput = true;
            writerSettings.ConformanceLevel = ConformanceLevel.Document;
            writerSettings.Indent = true;

            using (XmlWriter writer = XmlWriter.Create(_blogmlPath, writerSettings))
            {
                BlogMLSerializer.Serialize(writer, blog);
            }
        }

        private string UpdateCategoriesGetRef(ref List<BlogMLCategory> categories, string categoryName, DateTime created)
        {
            string categoryRef = null;

            if (categories.Count > 0)
            {
                for (int i = 0; i < categories.Count; i++)
                {
                    if (categories[i].Title == categoryName)
                    {
                        // if we have evidence of an earlier date for this category update it now
                        if (categories[i].DateCreated > created)
                        {
                            categories[i].DateCreated = created;
                            categories[i].DateModified = DateTime.UtcNow;
                        }

                        categoryRef = categories[i].ID;
                        break;
                    }
                }
            }

            // add a new category if not already in the list
            if (categoryRef == null)
            {
                // no ID in blogger so use a GUID
                categoryRef = Guid.NewGuid().ToString();

                BlogMLCategory category = new BlogMLCategory();
                category.Approved = true;
                category.DateCreated = created;
                category.DateModified = DateTime.UtcNow;
                category.Description = categoryName;
                category.ID = categoryRef;
                category.Title = categoryName;

                categories.Add(category);
            }

            return categoryRef;
        }

        private string UpdateAuthorsGetRef(ref List<BlogMLAuthor> authors, string authorName, string authorEmail, DateTime created)
        {
            string authorRef = null;

            // try to find an existing author
            if (authors.Count > 0)
            {
                for (int i = 0; i < authors.Count; i++)
                {
                    if ((authors[i].Title == authorName) && (authors[i].Email == authorEmail))
                    {
                        // if we have evidence of an earlier date for this author update it now
                        if (authors[i].DateCreated > created)
                        {
                            authors[i].DateCreated = created;
                            authors[i].DateModified = DateTime.UtcNow;
                        }

                        authorRef = authors[i].ID;
                        break;
                    }
                }
            }

            // add a new author if not already in the list
            if (authorRef == null)
            {
                // no ID in blogger so use a GUID
                authorRef = Guid.NewGuid().ToString();

                BlogMLAuthor author = new BlogMLAuthor();
                author.Approved = true;
                author.DateCreated = created;
                author.DateModified = DateTime.UtcNow;
                author.Email = authorEmail;
                author.ID = authorRef;
                author.Title = authorName;

                authors.Add(author);
            }

            return authorRef;
        }

        private void LogMessage(string message)
        {
            if (ConverterMessage != null)
            {
                ConverterMessage(this, new ConverterMessageEventArgs(message));
            }
        }
    }
}
