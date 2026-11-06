using System.Linq;

public static class Phrases {
    public static string OrgBlock { get { return Language.T("writes.organization_owns_repo_blocks_programs"); } }
    public static string WorkflowBlocked { get { return Language.T("writes.files_github_workflows_start_programs"); } }

    public static string Of(int code, string text, bool limit) {
        string t = (text ?? "").ToLowerInvariant();
        if (code == 401) return Language.T("writes.github_no_longer_recognizes_access");
        if (code == 429 || (code == 403 && (limit || t.Contains("rate limit")))) return Language.T("writes.github_limited_requests_nothing_changed");
        if (t.Contains("oauth app access restrictions")) return OrgBlock;
        if (t.Contains("saml")) return Language.T("writes.organization_requires_company_sign_authorize");
        if (t.Contains("workflow")) return WorkflowBlocked;
        if (t.Contains("archived")) return Language.T("writes.repo_archived_read_only_unarchive");
        if (t.Contains("protected branch")) return Language.T("writes.main_branch_protected_repos_rules");
        if (code == 403) return Language.T("writes.github_not_give_permission_role");
        if (code == 404) return Language.T("writes.isnt_github_dont_permission_see");
        if (code == 409) return t.Contains("empty") ? Language.T("writes.repo_still_empty") : Language.T("writes.changed_github_meantime_read_again");
        if (code == 422) {
            if (t.Contains("name already exists")) return Language.T("writes.repo_name_already_exists_choose");
            if (t.Contains("visibility")) return Language.T("writes.github_not_allow_visibility_change");
            if (t.Contains("already_exists") && t.Contains("releaseasset")) return Language.T("writes.release_already_attachment_same_name");
            if (t.Contains("already_exists")) return Language.T("writes.release_tag_already_exists_choose");
            if (t.Contains("not a valid") || t.Contains("could not resolve")) return Language.T("writes.github_not_recognize_name_check");
            return Language.T("writes.github_rejected_data_check_correct");
        }
        if (code >= 500) return Language.T("writes.github_problem_right_now_try");
        return Language.T("writes.github_answered_error", "code", code);
    }
}
