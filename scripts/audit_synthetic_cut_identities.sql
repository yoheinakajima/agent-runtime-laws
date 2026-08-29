WITH ordered AS (
  SELECT
    run_id,
    id,
    type,
    caused_by,
    ROW_NUMBER() OVER (PARTITION BY run_id ORDER BY seq) AS ordinal,
    COUNT(*) OVER (PARTITION BY run_id) AS n,
    LEAD(type) OVER (PARTITION BY run_id ORDER BY seq) AS next_type,
    LEAD(caused_by) OVER (PARTITION BY run_id ORDER BY seq) AS next_cause
  FROM events
),
request_adjacency AS (
  SELECT
    SUM(CASE
      WHEN next_type = 'llm.responded' AND next_cause = id THEN 1
      ELSE 0
    END) AS immediately_linked,
    SUM(CASE
      WHEN ordinal = n THEN 1
      ELSE 0
    END) AS terminal_unmatched,
    SUM(CASE
      WHEN NOT (next_type = 'llm.responded' AND next_cause = id)
       AND ordinal <> n THEN 1
      ELSE 0
    END) AS other_request_shapes
  FROM ordered
  WHERE type = 'llm.requested'
),
per_run AS (
  SELECT
    run_id,
    MAX(n) AS n,
    MIN(CASE WHEN type = 'llm.requested' THEN ordinal END) AS first_request,
    MAX(CASE WHEN type = 'llm.responded' THEN ordinal END) AS last_response,
    SUM(CASE WHEN type = 'llm.requested' THEN 1 ELSE 0 END) AS requests,
    SUM(CASE WHEN type = 'llm.responded' THEN 1 ELSE 0 END) AS responses
  FROM ordered
  GROUP BY run_id
)
SELECT
  (SELECT immediately_linked FROM request_adjacency) AS immediately_linked,
  (SELECT terminal_unmatched FROM request_adjacency) AS terminal_unmatched,
  (SELECT other_request_shapes FROM request_adjacency) AS other_request_shapes,
  SUM(n + 1) AS total_cuts,
  SUM(CASE
    WHEN first_request IS NULL THEN n + 1
    ELSE first_request
  END) AS ec_sound,
  SUM(requests) AS ec_unsound,
  SUM(CASE
    WHEN first_request IS NULL THEN 0
    ELSE n + 1 - first_request - requests
  END) AS ec_conditional,
  SUM(CASE
    WHEN requests = responses THEN COALESCE(last_response, 0)
    ELSE 0
  END) AS cw_unsound_closed_response,
  SUM(CASE
    WHEN requests > responses THEN n + 1
    ELSE 0
  END) AS cw_unsound_open_run,
  SUM(CASE
    WHEN requests > responses THEN n + 1
    ELSE COALESCE(last_response, 0)
  END) AS cw_unsound,
  SUM(CASE WHEN requests > 0 THEN 1 ELSE 0 END) AS cw_affected_runs,
  SUM(CASE
    WHEN requests > responses AND last_response IS NULL THEN 1
    ELSE 0
  END) AS open_without_prior_response,
  SUM(CASE
    WHEN requests > responses AND last_response IS NOT NULL THEN 1
    ELSE 0
  END) AS open_with_prior_response
FROM per_run;
