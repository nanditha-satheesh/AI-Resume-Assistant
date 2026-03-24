namespace AIResumeAssistant.PromptBuilder;

/// <summary>
/// Builds structured prompts that combine resume content with user queries
/// using role-based prompting for different professional perspectives.
/// </summary>
public static class ResumePromptBuilder
{
    /// <summary>
    /// Returns a system prompt tailored to the selected prompt mode.
    /// </summary>
    public static string BuildSystemPrompt(string promptMode)
    {
        return promptMode switch
        {
            "HR" =>
                "You are an experienced Human Resources professional with 15+ years of experience reviewing resumes. " +
                "You evaluate resumes for cultural fit, clarity, and compliance with job requirements. " +
                "Provide constructive, specific feedback. Format your response with clear headings and bullet points.",

            "Recruiter" =>
                "You are a senior technical recruiter who specializes in matching candidates to roles. " +
                "You focus on keywords, quantifiable achievements, and ATS (Applicant Tracking System) optimization. " +
                "Provide actionable advice to improve resume visibility. Format your response with clear headings and bullet points.",

            "CareerCoach" =>
                "You are a certified career coach who helps professionals at all levels advance their careers. " +
                "You focus on personal branding, career narrative, and strategic positioning. " +
                "Provide motivating, detailed guidance. Format your response with clear headings and bullet points.",

            _ =>
                "You are a professional resume writer and career advisor with expertise in crafting compelling resumes. " +
                "You help improve resume content, structure, and impact. " +
                "Provide clear, actionable suggestions. Format your response with clear headings and bullet points."
        };
    }

    /// <summary>
    /// Combines the resume text and user query into a structured user prompt.
    /// </summary>
    public static string BuildUserPrompt(string resumeText, string userQuery)
    {
        return $"""
            Here is my resume content:

            ---
            {resumeText}
            ---

            My request: {userQuery}
            """;
    }

    /// <summary>
    /// Returns available prompt modes with display names.
    /// </summary>
    public static Dictionary<string, string> GetAvailableModes()
    {
        return new Dictionary<string, string>
        {
            { "Default", "Resume Writer" },
            { "HR", "HR Professional" },
            { "Recruiter", "Technical Recruiter" },
            { "CareerCoach", "Career Coach" }
        };
    }

    /// <summary>
    /// Builds a prompt for ATS scoring with optional job description matching.
    /// </summary>
    public static string BuildAtsScorePrompt(string resumeText, string? jobDescription = null)
    {
        var jdSection = string.IsNullOrWhiteSpace(jobDescription)
            ? "No specific job description provided. Score against general best practices for a professional resume."
            : $"""
                Target Job Description:
                ---
                {jobDescription}
                ---
                """;

        return $$"""
            Analyze this resume and return a JSON response ONLY (no markdown, no explanation, no code blocks):
            {
              "overallScore": <0-100>,
              "formatScore": <0-100>,
              "keywordScore": <0-100>,
              "impactScore": <0-100>,
              "missingSkills": ["skill1", "skill2"],
              "matchPercentage": <0-100 if job description provided, otherwise null>,
              "suggestions": ["suggestion1", "suggestion2", "suggestion3"],
              "strongPoints": ["point1", "point2"]
            }

            Scoring criteria:
            - formatScore: Layout quality, proper sections (Contact, Summary, Experience, Education, Skills), readability, ATS-parseable format
            - keywordScore: Relevant industry keywords, action verbs, technical terms, tools and technologies
            - impactScore: Quantified achievements, metrics, measurable results, specific accomplishments

            {{jdSection}}

            Resume:
            ---
            {{resumeText}}
            ---
            """;
    }
}
