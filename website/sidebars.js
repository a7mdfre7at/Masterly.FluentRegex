// @ts-check

/**
 * A sidebar category whose label links to a generated overview page of cards (one per page, using each
 * page's front-matter description).
 * @param {string} label
 * @param {string} slug
 * @param {string} description
 * @param {string[]} items
 * @param {boolean} [collapsed]
 * @returns {import('@docusaurus/plugin-content-docs').SidebarItemCategory}
 */
function section(label, slug, description, items, collapsed = false) {
  return {
    type: 'category',
    label,
    collapsed,
    link: {type: 'generated-index', slug: `/category/${slug}`, title: label, description},
    items,
  };
}

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docs: [
    section('Getting Started', 'getting-started', 'Install the packages and build your first pattern.', [
      'overview',
      'installation',
      'quick-start',
    ]),
    section(
      'Building Patterns',
      'building-patterns',
      'Every building block of the fluent API, from anchors and character classes to groups, assertions and rules.',
      [
        'core-concepts',
        'basic-patterns',
        'character-classes',
        'quantifiers',
        'groups-and-backreferences',
        'assertions',
        'composing-patterns',
        'semantic-rules',
      ],
    ),
    section(
      'Matching and Extracting',
      'matching-and-extracting',
      'Test, find, replace and split text, bind matches to typed objects, and process input of any size.',
      ['matching', 'typed-extraction', 'fluent-replacements', 'templates', 'streaming', 'pii-redaction'],
    ),
    section('Pattern Library', 'pattern-library', 'Ready-made patterns for common formats, and full Unicode support.', [
      'common-patterns',
      'unicode',
    ]),
    section(
      'Prove and Optimize',
      'prove-and-optimize',
      'Prove patterns equivalent, read proven facts about what they match, optimize them safely and find ReDoS risks.',
      ['pattern-algebra', 'language-facts', 'optimizer', 'redos-safety'],
    ),
    section(
      'Test and Debug',
      'test-and-debug',
      'Explain, trace, highlight, draw and profile patterns, and test them with coverage, generated data and properties.',
      [
        'validation-and-debugging',
        'match-tracing',
        'highlighting',
        'diagrams',
        'backtracking-profiler',
        'regex-coverage',
        'test-data-generation',
        'property-testing',
        'xunit',
      ],
    ),
    section(
      'Generate and Translate',
      'generate-and-translate',
      'Turn regexes into fluent code, learn patterns from examples or plain English, and export them to other platforms.',
      ['regex-to-fluent-code', 'learning-from-examples', 'ai-drafting', 'export', 'sql-dialects', 'sql-like'],
    ),
    section(
      'Integrations',
      'integrations',
      'Entity Framework Core, ASP.NET Core, compile-time checks, the command-line tool and the browser playground.',
      ['entity-framework-core', 'aspnetcore', 'compile-time-checks', 'command-line-tool', 'playground'],
    ),
    'examples',
    section(
      'Reference',
      'reference',
      'The public API of every package, best practices, a regex cheat sheet and the 2.x to 3.x upgrade guide.',
      ['api-reference', 'best-practices', 'regex-cheat-sheet', 'upgrading-from-2x'],
    ),
  ],
};

export default sidebars;
