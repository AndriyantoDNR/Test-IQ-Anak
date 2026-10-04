# Database

PostgreSQL only. `SiapSDDbContext` migration creates child, catalog, question, blueprint, session, response and score tables. Unique `(AssessmentSessionId, QuestionId)` prevents duplicate submissions.
