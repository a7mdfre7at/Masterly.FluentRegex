import React, {useCallback, useEffect, useRef, useState} from 'react';
import clsx from 'clsx';
import CodeBlock from '@theme/CodeBlock';
import Icon from './Icon';
import styles from './PatternDemo.module.css';

// The code and every output below were produced by running this exact program against Masterly.FluentRegex 3.1.0.
const code = `Pattern invoice = Pattern.With
    .WordBoundary
    .Literal("INV-")
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4))
    .WordBoundary;

string text = "Paid INV-2024-0001 and INV-2025-0420, not INV-24-1.";
MatchCollection matches = invoice.Matches(text);
Console.WriteLine(invoice.Explain());`;

// invoice.ToString(), split into tokens for colouring.
const regexTokens = [
  {text: '\\b', kind: 'anchor'},
  {text: 'INV-', kind: 'literal'},
  {text: '(?<year>', kind: 'group'},
  {text: '\\d', kind: 'class'},
  {text: '{4}', kind: 'quantifier'},
  {text: ')', kind: 'group'},
  {text: '-', kind: 'literal'},
  {text: '(?<number>', kind: 'group'},
  {text: '\\d', kind: 'class'},
  {text: '{4}', kind: 'quantifier'},
  {text: ')', kind: 'group'},
  {text: '\\b', kind: 'anchor'},
];

// The sample text, split at the two matches (index 5 and 23, length 13). INV-24-1 is not matched.
const textSegments = [
  {text: 'Paid '},
  {match: {year: '2024', number: '0001'}},
  {text: ' and '},
  {match: {year: '2025', number: '0420'}},
  {text: ', not INV-24-1.'},
];

// invoice.Explain(), verbatim.
const explanation = `Pattern: \\bINV-(?<year>\\d{4})-(?<number>\\d{4})\\b

Explanation:
  - Word boundary
  - Literal 'I'
  - Literal 'N'
  - Literal 'V'
  - Character '-'
  - Named capturing group 'year'
    - Digit [0-9]
    - Exactly 4 times
    - End of group
  - Character '-'
  - Named capturing group 'number'
    - Digit [0-9]
    - Exactly 4 times
    - End of group
  - Word boundary`;

const stages = [
  {id: 'regex', call: 'invoice.ToString()', doneAt: 1},
  {id: 'matches', call: 'invoice.Matches(text)', doneAt: 2},
  {id: 'explain', call: 'invoice.Explain()', doneAt: 3},
];

const finalStep = 3;
const stepDelayMs = 750;

function stateOf(stage, step, running) {
  if (step >= stage.doneAt) {
    return 'done';
  }
  if (running && step === stage.doneAt - 1) {
    return 'running';
  }
  return 'pending';
}

function prefersReducedMotion() {
  return typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

function RegexOutput() {
  return (
    <code className={styles.regex}>
      {regexTokens.map((token, index) => (
        <span key={index} className={styles[`token_${token.kind}`]}>
          {token.text}
        </span>
      ))}
    </code>
  );
}

function MatchesOutput() {
  return (
    <>
      <p className={styles.sample}>
        {textSegments.map((segment, index) =>
          segment.match ? (
            <mark key={index} className={styles.match}>
              INV-<span className={styles.groupYear}>{segment.match.year}</span>-
              <span className={styles.groupNumber}>{segment.match.number}</span>
            </mark>
          ) : (
            <span key={index}>{segment.text}</span>
          ),
        )}
      </p>
      <p className={styles.legend}>
        <span className={styles.legendYear}>year</span>
        <span className={styles.legendNumber}>number</span>
        <span className={styles.legendNote}>named groups</span>
      </p>
    </>
  );
}

function ExplainOutput() {
  return (
    <pre className={styles.explain} tabIndex={0} aria-label="Explain() output">
      {explanation}
    </pre>
  );
}

const outputs = {
  regex: RegexOutput,
  matches: MatchesOutput,
  explain: ExplainOutput,
};

/**
 * A code window that "runs" a fluent pattern and reveals, one step at a time, the regex it builds, the matches it
 * finds in a sample text and its plain-English explanation. Every output is rendered from the start and only hidden,
 * so the animation never shifts the layout.
 */
export default function PatternDemo() {
  const [step, setStep] = useState(0);
  const [running, setRunning] = useState(false);
  const container = useRef(null);
  const started = useRef(false);

  const run = useCallback(() => {
    if (prefersReducedMotion()) {
      setStep(finalStep);
      return;
    }
    setStep(0);
    setRunning(true);
  }, []);

  useEffect(() => {
    if (!running) {
      return undefined;
    }
    if (step >= finalStep) {
      setRunning(false);
      return undefined;
    }
    const timer = setTimeout(() => setStep((current) => current + 1), stepDelayMs);
    return () => clearTimeout(timer);
  }, [running, step]);

  useEffect(() => {
    const element = container.current;
    if (!element || typeof IntersectionObserver === 'undefined') {
      run();
      return undefined;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting) && !started.current) {
          started.current = true;
          run();
          observer.disconnect();
        }
      },
      {threshold: 0.4},
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, [run]);

  const done = step >= finalStep;

  return (
    <div className={styles.window} ref={container}>
      <div className={styles.titleBar}>
        <span className={styles.dots} aria-hidden="true">
          <span />
          <span />
          <span />
        </span>
        <span className={styles.fileName}>InvoiceNumbers.cs</span>
        <button type="button" className={styles.runButton} onClick={run} disabled={running}>
          <Icon name={done ? 'replay' : 'play'} size={14} />
          <span>{done ? 'Run again' : running ? 'Running' : 'Run'}</span>
        </button>
      </div>

      <CodeBlock language="csharp" className={styles.code}>
        {code}
      </CodeBlock>

      <div className={styles.console}>
        <div className={styles.consoleHeader}>
          <span>Output</span>
          <span className={styles.status} role="status" aria-live="polite">
            {done ? (
              <>
                <span className={styles.statusMatches}>2 matches</span> · 2 named groups
              </>
            ) : running ? (
              'Running…'
            ) : (
              'Ready'
            )}
          </span>
        </div>
        <ol className={styles.stages} aria-label="Program output">
          {stages.map((stage) => {
            const state = stateOf(stage, step, running);
            const Output = outputs[stage.id];
            return (
              <li key={stage.id} className={clsx(styles.stage, styles[state])}>
                <span className={styles.call}>
                  <span className={styles.marker} aria-hidden="true" />
                  {stage.call}
                </span>
                {/* Hidden outputs use visibility: hidden, so they keep their space and are skipped by
                    assistive technology and keyboard focus until revealed. */}
                <div className={styles.output}>
                  <Output />
                </div>
              </li>
            );
          })}
        </ol>
      </div>
    </div>
  );
}
