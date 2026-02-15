using System;
using System.Collections.Generic;

namespace ToodledoConsole
{
    public class TokenStorage
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class TokenResponse 
    { 
        public string access_token { get; set; } = string.Empty;
        public string refresh_token { get; set; } = string.Empty;
    }

    public class ToodledoTask 
    { 
        public string id { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public int priority { get; set; }
        public long folder { get; set; }
        public long context { get; set; }
        public int star { get; set; }
        public long duedate { get; set; }
        public int status { get; set; }
        public string tag { get; set; } = string.Empty;
        public string note { get; set; } = string.Empty;
        public long added { get; set; }
        public long location { get; set; }
    }

    public class ToodledoFolder
    {
        public long id { get; set; }
        public string name { get; set; } = string.Empty;
    }

    public class ToodledoContext
    {
        public long id { get; set; }
        public string name { get; set; } = string.Empty;
    }

    public class ToodledoLocation
    {
        public long id { get; set; }
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public double lat { get; set; }
        public double lon { get; set; }
    }

    public class FilterCriteria
    {
        public int? Priority { get; set; }
        public string? FolderName { get; set; }
        public long? FolderId { get; set; }
        public string? ContextName { get; set; }
        public long? ContextId { get; set; }
        public int? Starred { get; set; }
        public string? DueDateShortcut { get; set; }
        public int? Status { get; set; }
        public string? Tag { get; set; }
        public string? Note { get; set; }
        public string? SearchTerm { get; set; }
        public bool IsActive => Priority.HasValue || FolderId.HasValue || ContextId.HasValue || 
                            Starred.HasValue || !string.IsNullOrEmpty(DueDateShortcut) || 
                            Status.HasValue || !string.IsNullOrEmpty(Tag) || !string.IsNullOrEmpty(Note) || !string.IsNullOrEmpty(SearchTerm);
    }
}
