import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import CodeBlock from '@theme/CodeBlock';
import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';
import Icon from '@site/src/components/home/Icon';
import CopyCommand from '@site/src/components/home/CopyCommand';
import PatternDemo from '@site/src/components/home/PatternDemo';
import styles from './index.module.css';

// Every sample below was compiled and run against Masterly.FluentRegex 3.1.0; the comments show the real output.
const patternStyles = [
  {
    value: 'builder',
    label: 'Fluent builder',
    caption: 'Compose a pattern from named building blocks. Every call returns a new, immutable pattern.',
    to: '/docs/core-concepts',
    code: `Pattern semver = Pattern.With
    .StartOfLine
    .NamedGroup("major", Pattern.With.Digit.Repeat.OneOrMore)
    .Literal(".")
    .NamedGroup("minor", Pattern.With.Digit.Repeat.OneOrMore)
    .Literal(".")
    .NamedGroup("patch", Pattern.With.Digit.Repeat.OneOrMore)
    .EndOfLine;

semver.ToString();          // ^(?<major>\\d+)\\.(?<minor>\\d+)\\.(?<patch>\\d+)$
semver.IsMatch("3.1.0");    // true
semver.IsMatch("3.1");      // false`,
  },
  {
    value: 'from-regex',
    label: 'From a regex',
    caption: 'Parse a regex you already have, then turn it into readable fluent C# you can keep in source control.',
    to: '/docs/regex-to-fluent-code',
    code: `Pattern legacy = Pattern.FromRegex(@"^(?<area>\\d{3})-(?<line>\\d{4})$");

string code = legacy.ToFluentCode();
// Pattern.With
//     .StartOfLine
//     .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
//     .Literal("-")
//     .NamedGroup("line", Pattern.With.Digit.Repeat.Times(4))
//     .EndOfLine`,
  },
  {
    value: 'template',
    label: 'Typed template',
    caption: 'Describe a line the way you would write an interpolated string. You get a typed parser and formatter.',
    to: '/docs/templates',
    code: `public enum LogLevel { Debug, Info, Warn, Error }
public record LogLine(DateTime Date, LogLevel Level, string User, string Message);

Template<LogLine> log = Template.Of<LogLine>("{Date:yyyy-MM-dd} [{Level}] {User}: {Message}");

LogLine line = log.Parse("2026-11-02 [WARN] ahmad: disk at 91%");
// line.Level == LogLevel.Warn, line.User == "ahmad", line.Message == "disk at 91%"

string text = log.Format(line);   // "2026-11-02 [Warn] ahmad: disk at 91%"`,
  },
  {
    value: 'learn',
    label: 'Learn from examples',
    caption: 'Give strings that must match and strings that must not. You get a readable, anchored pattern.',
    to: '/docs/learning-from-examples',
    code: `Pattern learned = Pattern.Learn(
    match: new[] { "INV-2024-0001", "INV-2025-0420" },
    reject: new[] { "INV-24-1", "inv-2024-0001" });

learned.ToString();                  // ^INV-\\d{4}-\\d{4}$
learned.IsMatch("INV-2026-0007");    // true`,
  },
  {
    value: 'common',
    label: 'Common patterns',
    caption: 'Start from a ready-made pattern, and add checks that a regex alone cannot express.',
    to: '/docs/common-patterns',
    code: `CommonPatterns.Email().IsMatch("ahmad@example.com");              // true
CommonPatterns.IPv4(DigitMode.Ascii).IsMatch("192.168.0.1");      // true

ConstrainedPattern card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");
card.IsMatch("4111 1111 1111 1111");   // true
card.IsMatch("4111 1111 1111 1112");   // false: the shape is right, the checksum is not`,
  },
];

const features = [
  {
    icon: 'compare',
    title: 'Prove two patterns equal',
    text: 'Pattern.Compare decides equivalence, subset and overlap exactly. When two patterns differ, you get the shortest string that tells them apart.',
    to: '/docs/pattern-algebra',
    size: 'wide',
    visual: 'compare',
  },
  {
    icon: 'zap',
    title: 'Optimize with proof',
    text: 'Optimize() rewrites cat|car|cab to ca[trb], then proves the result matches exactly the same strings.',
    to: '/docs/optimizer',
  },
  {
    icon: 'shield',
    title: 'Catch ReDoS early',
    text: 'AnalyzeSafety() finds repetitions that backtrack exponentially, and can confirm each one with a timed attack.',
    to: '/docs/redos-safety',
  },
  {
    icon: 'braces',
    title: 'Typed extraction',
    text: 'Extract<T>() binds named groups straight onto records and classes, converting each value to its member type.',
    to: '/docs/typed-extraction',
  },
  {
    icon: 'code',
    title: 'Checked as you type',
    text: 'The bundled source generator and analyzers flag invalid patterns, ReDoS risks and group-name typos at build time.',
    to: '/docs/compile-time-checks',
  },
  {
    icon: 'target',
    title: 'Coverage and test data',
    text: 'MeasureCoverage() shows the branches your tests never hit. GenerateMatching() and GenerateNonMatching() produce samples and near misses.',
    to: '/docs/regex-coverage',
  },
  {
    icon: 'database',
    title: 'Run it in the database',
    text: 'The same pattern becomes each database’s own regex, with the exact .NET meaning. The EF Core package runs it inside LINQ queries and CHECK constraints.',
    to: '/docs/entity-framework-core',
    size: 'wide',
    visual: 'sql',
  },
  {
    icon: 'route',
    title: 'Trace every match',
    text: 'Trace(input) shows which construct matched which text, or exactly where and why matching failed.',
    to: '/docs/match-tracing',
  },
  {
    icon: 'globe',
    title: 'One pattern, every platform',
    text: 'ToJavaScript(), JSON Schema and TypeScript output keep the .NET meaning in browsers and API contracts.',
    to: '/docs/export',
  },
  {
    icon: 'eyeOff',
    title: 'Redact personal data',
    text: 'Redactor.For(...) finds emails, phone numbers and other personal data in text, and masks each with the style you choose.',
    to: '/docs/pii-redaction',
  },
];

const packages = [
  {icon: 'box', name: 'Masterly.FluentRegex', role: 'Core', text: 'The fluent builder, matching, extraction, templates, pattern algebra, optimizer, ReDoS analysis and export. Ships the source generator and analyzers.', target: 'netstandard2.1 · net8.0 · net10.0', deps: 'No dependencies', to: '/docs/installation'},
  {icon: 'box', name: 'Masterly.FluentRegex.EntityFrameworkCore', role: 'EF Core', text: 'Patterns in LINQ run as the database’s own regex, with proven index hints and CHECK constraints.', target: 'net8.0 · net10.0', deps: 'EF Core Relational 8 / 10', to: '/docs/entity-framework-core'},
  {icon: 'box', name: 'Masterly.FluentRegex.AspNetCore', role: 'ASP.NET Core', text: 'Register named patterns once and use them as route constraints, such as {id:pattern(sku)}.', target: 'net8.0 · net10.0', deps: 'ASP.NET Core', to: '/docs/aspnetcore'},
  {icon: 'box', name: 'Masterly.FluentRegex.Xunit', role: 'Testing', text: '[MatchingData] and [NonMatchingData] feed xUnit v3 theories with matches and near misses.', target: 'net8.0 · net10.0', deps: 'xunit.v3.extensibility.core', to: '/docs/xunit'},
  {icon: 'box', name: 'Masterly.FluentRegex.AI', role: 'AI drafting', text: 'Describe a pattern in plain language. Claude drafts it, and every draft is verified locally first.', target: 'net8.0 · net10.0', deps: 'Core only', to: '/docs/ai-drafting'},
  {icon: 'terminal', name: 'Masterly.FluentRegex.Tool', role: 'Command line', text: 'The fluentregex .NET tool compares, optimizes, exports and scans a codebase for ReDoS in CI.', target: 'net8.0', deps: 'dotnet tool', to: '/docs/command-line-tool'},
];

// Pattern.Compare(Pattern.FromRegex(@"\d{3}"), Pattern.FromRegex(@"[0-9]{3}")), as printed by ToString().
const comparison = {
  left: '\\d{3}',
  right: '[0-9]{3}',
  relation: 'ProperSuperset',
  counterexample: '"00٠"',
  common: '"000"',
};

// pattern.ToSqlCondition("Sku", flavor) for ^[A-Z]{3}-[0-9]{4}$, one row per RegexFlavor.
const sqlRows = [
  {dialect: 'PostgreSQL', sql: "Sku ~ '\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\Z)'"},
  {dialect: 'MySQL', sql: "REGEXP_LIKE(Sku, '(?-i)\\\\A[A-Z]{3}-[0-9]{4}(?=\\\\n?\\\\z)')"},
  {dialect: 'SQL Server 2025', sql: "REGEXP_LIKE(Sku, N'\\A[A-Z]{3}-[0-9]{4}\\n?\\z')"},
  {dialect: 'SQLite', sql: "Sku REGEXP '^[A-Z]{3}-[0-9]{4}$'"},
];

function Hero() {
  const {siteConfig} = useDocusaurusContext();
  const {packageVersion, repositoryUrl, playgroundUrl} = siteConfig.customFields;
  return (
    <header className={styles.hero}>
      <div className={styles.heroBackdrop} aria-hidden="true" />
      <div className={clsx('container', styles.heroGrid)}>
        <div className={styles.heroCopy}>
          <Link className={styles.eyebrow} href={`${repositoryUrl}/blob/master/CHANGELOG.md`}>
            <span className={styles.eyebrowVersion}>v{packageVersion}</span>
            <span>Open source · MIT · .NET 8 / 10 · .NET Standard 2.1</span>
            <Icon name="arrowRight" size={14} />
          </Link>
          <Heading as="h1" className={styles.heroTitle}>
            Regular expressions you can <span className={styles.heroAccent}>read, prove and ship.</span>
          </Heading>
          <p className={styles.heroLead}>
            Build .NET regexes from readable, immutable building blocks. Prove two patterns equivalent, catch ReDoS
            before it ships, test with generated data, and run the same pattern in JavaScript, JSON Schema or your
            database.
          </p>
          <div className={styles.heroActions}>
            <Link className={clsx('button button--primary button--lg', styles.primaryButton)} to="/docs/quick-start">
              Get started
              <Icon name="arrowRight" size={18} />
            </Link>
            <Link className={clsx('button button--lg', styles.secondaryButton)} to="/docs/overview">
              Read the docs
            </Link>
          </div>
          <div className={styles.heroInstallRow}>
            <CopyCommand command="dotnet add package Masterly.FluentRegex" className={styles.heroInstall} />
            <Link className={styles.playgroundLink} href={playgroundUrl}>
              <Icon name="play" size={14} />
              Try it in the playground
            </Link>
          </div>
        </div>
        <div className={styles.heroDemo}>
          <PatternDemo />
        </div>
      </div>
    </header>
  );
}

function Stats() {
  const stats = [
    {value: '0', label: 'dependencies in the core package'},
    {value: '6', label: 'packages, one shared version'},
    {value: '5', label: 'SQL dialects, plus JavaScript and JSON Schema'},
    {value: '.NET Standard 2.1', label: 'plus native .NET 8 and .NET 10 builds'},
  ];
  return (
    <section className={styles.stats} aria-label="At a glance">
      <div className={clsx('container', styles.statsGrid)}>
        {stats.map((stat) => (
          <div key={stat.label} className={styles.stat}>
            <div className={styles.statValue}>{stat.value}</div>
            <div className={styles.statLabel}>{stat.label}</div>
          </div>
        ))}
      </div>
    </section>
  );
}

function SectionHeader({eyebrow, title, children}) {
  return (
    <div className={styles.sectionHeader}>
      <div className={styles.sectionEyebrow}>{eyebrow}</div>
      <Heading as="h2" className={styles.sectionTitle}>
        {title}
      </Heading>
      {children && <p className={styles.sectionLead}>{children}</p>}
    </div>
  );
}

function PatternStyles() {
  return (
    <section className={styles.section}>
      <div className="container">
        <SectionHeader eyebrow="Write" title="Five ways to get a pattern, one Pattern type">
          Build it, convert it, describe it, learn it or pick it from the library. Whichever way you start, you get the
          same immutable Pattern that matches, explains, proves and exports.
        </SectionHeader>
        <div className={styles.tabsCard}>
          <Tabs groupId="pattern-style" className={styles.tabs}>
            {patternStyles.map((style) => (
              <TabItem key={style.value} value={style.value} label={style.label}>
                <p className={styles.tabCaption}>
                  {style.caption}{' '}
                  <Link to={style.to} className={styles.inlineLink}>
                    Learn more
                    <Icon name="arrowRight" size={14} />
                  </Link>
                </p>
                <CodeBlock language="csharp">{style.code}</CodeBlock>
              </TabItem>
            ))}
          </Tabs>
        </div>
      </div>
    </section>
  );
}

function CompareVisual() {
  return (
    <div className={styles.compare}>
      <div className={styles.compareInputs}>
        <span className={styles.compareSide}>
          <span className={styles.compareLabel}>left</span>
          <code>{comparison.left}</code>
        </span>
        <span className={styles.compareVersus} aria-hidden="true">
          vs
        </span>
        <span className={styles.compareSide}>
          <span className={styles.compareLabel}>right</span>
          <code>{comparison.right}</code>
        </span>
      </div>
      <dl className={styles.compareResult}>
        <div>
          <dt>Relation</dt>
          <dd>
            <code>{comparison.relation}</code>
          </dd>
        </div>
        <div className={styles.compareWitness}>
          <dt>Shortest counterexample</dt>
          <dd>
            <code>{comparison.counterexample}</code>
            <span className={styles.compareVerdicts}>
              <span className={styles.accepts}>left accepts</span>
              <span className={styles.rejects}>right rejects</span>
            </span>
          </dd>
        </div>
        <div>
          <dt>Both accept</dt>
          <dd>
            <code>{comparison.common}</code>
          </dd>
        </div>
      </dl>
      <p className={styles.visualNote}>٠ is an Arabic-Indic zero: \d matches every Unicode decimal digit.</p>
    </div>
  );
}

function SqlVisual() {
  return (
    <table className={styles.sql}>
      <caption className={styles.sqlCaption}>
        <code>ToSqlCondition("Sku", flavor)</code> for <code>^[A-Z]{'{3}'}-[0-9]{'{4}'}$</code>
      </caption>
      <thead>
        <tr>
          <th scope="col">Dialect</th>
          <th scope="col">SQL</th>
        </tr>
      </thead>
      <tbody>
        {sqlRows.map((row) => (
          <tr key={row.dialect}>
            <td>{row.dialect}</td>
            <td>
              <code>{row.sql}</code>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function Features() {
  return (
    <section className={clsx(styles.section, styles.sectionAlt)}>
      <div className="container">
        <SectionHeader eyebrow="Capabilities" title="More than a builder">
          Start with a readable pattern. Then prove it, harden it, test it and take it wherever your data lives, without
          rewriting it for each place.
        </SectionHeader>
        <div className={styles.bento}>
          {features.map((feature) => (
            <Link
              key={feature.title}
              to={feature.to}
              className={clsx(styles.featureCard, feature.size === 'wide' && styles.featureWide)}>
              <div className={styles.featureBody}>
                <span className={styles.featureIcon}>
                  <Icon name={feature.icon} size={20} />
                </span>
                <Heading as="h3" className={styles.featureTitle}>
                  {feature.title}
                </Heading>
                <p className={styles.featureText}>{feature.text}</p>
                <span className={styles.featureMore}>
                  Read the guide
                  <Icon name="arrowRight" size={14} />
                </span>
              </div>
              {feature.visual === 'compare' && <CompareVisual />}
              {feature.visual === 'sql' && <SqlVisual />}
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}

function Packages() {
  return (
    <section className={styles.section}>
      <div className="container">
        <SectionHeader eyebrow="Packages" title="Install the core. Add only what you use.">
          All six packages ship together with one version number. Each companion library depends only on the core and
          the framework it plugs into.
        </SectionHeader>
        <div className={styles.packages}>
          {packages.map((pkg) => (
            <Link key={pkg.name} to={pkg.to} className={styles.packageCard}>
              <div className={styles.packageTop}>
                <span className={styles.packageIcon}>
                  <Icon name={pkg.icon} size={18} />
                </span>
                <span className={styles.packageRole}>{pkg.role}</span>
              </div>
              <code className={styles.packageName}>{pkg.name}</code>
              <p className={styles.packageText}>{pkg.text}</p>
              <div className={styles.packageMeta}>
                <span>{pkg.target}</span>
                <span>{pkg.deps}</span>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}

function CallToAction() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <section className={styles.cta}>
      <div className={clsx('container', styles.ctaInner)}>
        <Heading as="h2" className={styles.ctaTitle}>
          Write your first readable pattern in five minutes.
        </Heading>
        <p className={styles.ctaText}>
          The Quick Start builds a pattern, tests it, extracts matches with named groups and explains it. The
          playground runs every feature in your browser.
        </p>
        <div className={styles.heroActions}>
          <Link className={clsx('button button--primary button--lg', styles.primaryButton)} to="/docs/quick-start">
            Open the Quick Start
            <Icon name="arrowRight" size={18} />
          </Link>
          <Link className={clsx('button button--lg', styles.secondaryButton)} href={siteConfig.customFields.playgroundUrl}>
            <Icon name="play" size={16} />
            Open the playground
          </Link>
        </div>
      </div>
    </section>
  );
}

export default function Home() {
  return (
    <Layout description="A fluent, immutable API for building, proving, testing and shipping .NET regular expressions: equivalence proofs, a verified optimizer, ReDoS analysis, typed templates, SQL dialects and EF Core.">
      <Hero />
      <main>
        <Stats />
        <PatternStyles />
        <Features />
        <Packages />
        <CallToAction />
      </main>
    </Layout>
  );
}
