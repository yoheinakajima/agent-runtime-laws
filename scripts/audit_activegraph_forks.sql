WITH
fork_rows AS (
  SELECT
    run_id AS child_run_id,
    parent_run_id,
    forked_at_event_id
  FROM runs
  WHERE parent_run_id IS NOT NULL
),
cutoffs AS (
  SELECT
    f.child_run_id,
    f.parent_run_id,
    f.forked_at_event_id,
    parent_cut.seq AS parent_cut_seq
  FROM fork_rows AS f
  LEFT JOIN events AS parent_cut
    ON parent_cut.run_id = f.parent_run_id
   AND parent_cut.id = f.forked_at_event_id
),
parent_prefix AS (
  SELECT
    c.child_run_id,
    ROW_NUMBER() OVER (
      PARTITION BY c.child_run_id
      ORDER BY parent_event.seq
    ) AS ordinal,
    parent_event.id,
    parent_event.type,
    parent_event.actor,
    parent_event.payload,
    parent_event.frame_id,
    parent_event.caused_by,
    parent_event.timestamp
  FROM cutoffs AS c
  JOIN events AS parent_event
    ON parent_event.run_id = c.parent_run_id
   AND parent_event.seq <= c.parent_cut_seq
),
child_trace AS (
  SELECT
    c.child_run_id,
    ROW_NUMBER() OVER (
      PARTITION BY c.child_run_id
      ORDER BY child_event.seq
    ) AS ordinal,
    child_event.id,
    child_event.type,
    child_event.actor,
    child_event.payload,
    child_event.frame_id,
    child_event.caused_by,
    child_event.timestamp
  FROM cutoffs AS c
  JOIN events AS child_event
    ON child_event.run_id = c.child_run_id
),
comparisons AS (
  SELECT
    parent.child_run_id,
    parent.ordinal,
    CASE
      WHEN child.ordinal IS NULL THEN 1
      WHEN parent.id IS NOT child.id THEN 1
      WHEN parent.type IS NOT child.type THEN 1
      WHEN parent.actor IS NOT child.actor THEN 1
      WHEN parent.payload IS NOT child.payload THEN 1
      WHEN parent.frame_id IS NOT child.frame_id THEN 1
      WHEN parent.caused_by IS NOT child.caused_by THEN 1
      WHEN parent.timestamp IS NOT child.timestamp THEN 1
      ELSE 0
    END AS mismatch
  FROM parent_prefix AS parent
  LEFT JOIN child_trace AS child
    ON child.child_run_id = parent.child_run_id
   AND child.ordinal = parent.ordinal
)
SELECT
  (SELECT COUNT(*) FROM fork_rows) AS forks,
  (SELECT COUNT(*) FROM cutoffs WHERE parent_cut_seq IS NULL) AS missing_cut_events,
  COUNT(*) AS compared_prefix_events,
  COALESCE(SUM(mismatch), 0) AS prefix_mismatches,
  COUNT(DISTINCT CASE WHEN mismatch = 1 THEN child_run_id END) AS forks_with_mismatch
FROM comparisons;

WITH
fork_rows AS (
  SELECT
    run_id AS child_run_id,
    parent_run_id,
    forked_at_event_id
  FROM runs
  WHERE parent_run_id IS NOT NULL
),
cutoffs AS (
  SELECT
    f.child_run_id,
    f.parent_run_id,
    f.forked_at_event_id,
    parent_cut.seq AS parent_cut_seq,
    parent_cut.type AS cut_event_type
  FROM fork_rows AS f
  LEFT JOIN events AS parent_cut
    ON parent_cut.run_id = f.parent_run_id
   AND parent_cut.id = f.forked_at_event_id
),
retained_requests AS (
  SELECT
    c.child_run_id,
    request_event.run_id,
    request_event.id,
    request_event.type,
    request_event.seq
  FROM cutoffs AS c
  JOIN events AS request_event
    ON request_event.run_id = c.parent_run_id
   AND request_event.seq <= c.parent_cut_seq
   AND request_event.type IN (
     'llm.requested',
     'model.requested',
     'embedding.requested',
     'tool.requested',
     'human.requested'
   )
),
unresolved_requests AS (
  SELECT retained.*
  FROM retained_requests AS retained
  WHERE NOT EXISTS (
    SELECT 1
    FROM events AS outcome
    JOIN cutoffs AS c
      ON c.child_run_id = retained.child_run_id
    WHERE outcome.run_id = retained.run_id
      AND outcome.seq <= c.parent_cut_seq
      AND outcome.caused_by = retained.id
      AND outcome.type IN (
        'llm.responded',
        'llm.failed',
        'model.responded',
        'model.failed',
        'embedding.responded',
        'embedding.failed',
        'tool.responded',
        'tool.returned',
        'tool.failed',
        'human.responded',
        'human.decided',
        'human.failed'
      )
  )
)
SELECT
  (SELECT COUNT(*) FROM fork_rows) AS forks,
  (SELECT COUNT(DISTINCT parent_run_id) FROM fork_rows) AS distinct_parents,
  (SELECT COUNT(*) FROM runs WHERE parent_run_id IN (SELECT child_run_id FROM fork_rows)) AS nested_forks,
  (SELECT json_group_object(cut_event_type, count_per_type)
     FROM (
       SELECT cut_event_type, COUNT(*) AS count_per_type
       FROM cutoffs
       GROUP BY cut_event_type
     )) AS cut_event_types,
  (SELECT COUNT(*) FROM retained_requests) AS retained_external_requests,
  (SELECT COUNT(*) FROM unresolved_requests) AS unresolved_retained_requests;
