using System;

namespace MyWebForms
{
    /// <summary>
    /// Carries the HN item ID when a user clicks the "N comments" link on a
    /// story row, causing the host page to load the story detail panel.
    /// </summary>
    public sealed class StorySelectedEventArgs : EventArgs
    {
        /// <summary>The HN item ID of the selected story.</summary>
        public int ItemId { get; private set; }

        public StorySelectedEventArgs(int itemId)
        {
            ItemId = itemId;
        }
    }

    /// <summary>
    /// Carries the HN username when a user clicks an author link on a story
    /// row or comment, causing the host page to load the user profile panel.
    /// </summary>
    public sealed class AuthorSelectedEventArgs : EventArgs
    {
        /// <summary>The HN username of the selected author.</summary>
        public string Username { get; private set; }

        public AuthorSelectedEventArgs(string username)
        {
            Username = username;
        }
    }
}
