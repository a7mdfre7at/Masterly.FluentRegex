import React from 'react';
import Link from '@docusaurus/Link';
import styles from './ConceptFlow.module.css';

const stages = [
  {
    step: '1',
    title: 'Build',
    text: 'Compose a pattern from readable building blocks, convert a regex you already have, or start from a ready-made one.',
    apis: ['Pattern.With', 'Pattern.FromRegex(...)', 'CommonPatterns.Email()'],
    to: '/docs/core-concepts',
  },
  {
    step: '2',
    title: 'Match and extract',
    text: 'Test, find, replace and split text, and bind what you capture straight onto typed objects.',
    apis: ['pattern.IsMatch(input)', 'pattern.Extract<T>(input)', 'Template.Of<T>(...)'],
    to: '/docs/matching',
  },
  {
    step: '3',
    title: 'Prove and test',
    text: 'Prove two patterns equivalent, see which branches your tests miss, and generate matching and failing data.',
    apis: ['Pattern.Compare(a, b)', 'pattern.MeasureCoverage(...)', 'pattern.GenerateMatching()'],
    to: '/docs/pattern-algebra',
  },
  {
    step: '4',
    title: 'Ship anywhere',
    text: 'Export the same pattern to JavaScript, JSON Schema and SQL, or run it inside Entity Framework Core queries.',
    apis: ['pattern.ToJavaScript()', 'pattern.ToSqlCondition(...)', 'EF.Functions.Matches(...)'],
    to: '/docs/export',
  },
];

const helpers = [
  {api: '.Explain()', text: 'describe it in English', to: '/docs/validation-and-debugging'},
  {api: '.AnalyzeSafety()', text: 'find ReDoS risks', to: '/docs/redos-safety'},
  {api: '.Optimize()', text: 'simplify, with proof', to: '/docs/optimizer'},
  {api: '.Must(...)', text: 'add semantic checks', to: '/docs/semantic-rules'},
  {api: '.WithAsciiDigits()', text: 'make \\d mean 0-9', to: '/docs/unicode'},
];

// Long member names such as pattern.MeasureCoverage(...) may wrap only after a dot, never mid-word.
function withBreaksAfterDots(api) {
  return api.split(/(?<=\.)/).map((part, index) => (
    <React.Fragment key={index}>
      {index > 0 && <wbr />}
      {part}
    </React.Fragment>
  ));
}

/**
 * The four stages of working with patterns, plus the helpers that apply to any pattern.
 */
export default function ConceptFlow() {
  return (
    <div className={styles.flow}>
      <ol className={styles.stages}>
        {stages.map((stage) => (
          <li key={stage.title} className={styles.stage}>
            <Link to={stage.to} className={styles.stageLink}>
              <span className={styles.step} aria-hidden="true">
                {stage.step}
              </span>
              <span className={styles.title}>{stage.title}</span>
              <span className={styles.text}>{stage.text}</span>
              <span className={styles.apis}>
                {stage.apis.map((api) => (
                  <code key={api}>{withBreaksAfterDots(api)}</code>
                ))}
              </span>
            </Link>
          </li>
        ))}
      </ol>
      <div className={styles.wrappers}>
        <span className={styles.wrappersLabel}>Works on any pattern</span>
        <ul className={styles.wrapperList}>
          {helpers.map((helper) => (
            <li key={helper.api}>
              <Link to={helper.to} className={styles.wrapper}>
                <code>{helper.api}</code>
                <span>{helper.text}</span>
              </Link>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
