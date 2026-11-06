public static class Permissions {
    public static string Role(RepoFacts f) { return f.Admin ? Language.T("writes.administer") : f.Maintain ? Language.T("writes.manage") : f.Push ? Language.T("writes.write") : f.Triage ? Language.T("writes.sort_reports") : f.Pull ? Language.T("writes.only_read") : Language.T("writes.no_role"); }

    public static string Because(RepoAction a, RepoFacts f) {
        if (f.Archived && a != RepoAction.Archive && a != RepoAction.Deletion) return Language.T("writes.repo_archived_read_only_unarchive");
        string role = Language.T("writes.here_you", "f", Role(f));
        switch (a) {
            case RepoAction.Content:
            case RepoAction.Release:
                return f.Push ? null : Language.T("writes.need_permission_write_whoever_administers", "role", role);
            case RepoAction.Description:
                return f.Admin || f.Maintain ? null : Language.T("writes.need_administrator_manager_repo", "role", role);
            default:
                return f.Admin ? null : Language.T("writes.need_administrator_repo", "role", role);
        }
    }

    public static void Allow(RepoAction a, RepoFacts f) {
        string p = Because(a, f);
        if (p != null) throw new WriteException(403, p);
    }
}
