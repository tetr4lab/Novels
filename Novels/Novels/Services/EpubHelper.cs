using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;

namespace QuickEPUB {
    /// <summary>
    /// Helper class for Epub operations.
    /// </summary>
    public static class EpubHelper {
        /// <summary>
        /// Adds a chapter to the book.
        /// </summary>
        /// <param name="book"></param>
        /// <param name="heading1"></param>
        /// <param name="heading2"></param>
        /// <param name="content"></param>
        /// <param name="note"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AddChapter (this Epub book, string? heading1, string? heading2, string? heading3, string? content, string? note = null, string? preface = null) {
            if (string.IsNullOrEmpty (content)) {
                throw new ArgumentNullException (nameof (content));
            }
            if (string.IsNullOrEmpty (heading1) && string.IsNullOrEmpty (heading2) && string.IsNullOrEmpty (heading3)) {
                throw new ArgumentNullException ($"{nameof (heading1)} or {nameof (heading2)} or {nameof (heading3)}");
            }
            if (heading1 == "") { heading1 = null; }
            if (heading2 == "") { heading2 = null; }
            book.AddSection (heading1 ?? heading2 ?? heading3, string.Join ('\n', [
                string.IsNullOrEmpty (heading1) ? "" : $"<h1>{heading1}</h1>",
                string.IsNullOrEmpty (heading2) ? "" : $"<h2>{heading2}</h2>",
                string.IsNullOrEmpty (heading3) ? "" : $"<h3>{heading3}</h3>",
                string.IsNullOrEmpty (preface) ? "" : $"<div class=\"preface\">{preface}</div>",
                string.IsNullOrEmpty (content) ? "" : $"<div class=\"chapter-body\">{content}</div>",
                string.IsNullOrEmpty (note) ? "" : $"<div class=\"note\">{note}</div>",
            ]), "book-style.css");
        }
        /// <summary>Adds a title page to the book.</summary>
        /// <param name="book"></param>
        public static void AddTitle (this Epub book) {
            book.AddSection ( "本扉", $"""
                <div class="title-page">
                    <div class="title">{book.Title}</div>
                    <div class="author">{book.Author}</div>
                </div>
                """, "book-style.css");
        }
    }
}
