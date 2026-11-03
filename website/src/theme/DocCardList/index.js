import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import {
  filterDocCardListItems,
  useCurrentSidebarSiblings,
  useDocById,
} from '@docusaurus/plugin-content-docs/client';
import Icon from '@site/src/components/home/Icon';
import styles from './styles.module.css';

// Ejected from @docusaurus/theme-classic (marked safe to swizzle). Renders the cards on generated category pages
// with the site's icon set and full page descriptions instead of emoji and truncated text.
const iconByDocId = {
  'overview': 'layers',
  'installation': 'box',
  'quick-start': 'zap',
  'core-concepts': 'regex',
  'basic-patterns': 'regex',
  'character-classes': 'type',
  'quantifiers': 'repeat',
  'groups-and-backreferences': 'parentheses',
  'assertions': 'eye',
  'composing-patterns': 'layers',
  'semantic-rules': 'checks',
  'matching': 'search',
  'typed-extraction': 'braces',
  'fluent-replacements': 'shuffle',
  'templates': 'braces',
  'streaming': 'waves',
  'pii-redaction': 'eyeOff',
  'common-patterns': 'book',
  'unicode': 'languages',
  'pattern-algebra': 'compare',
  'language-facts': 'ruler',
  'optimizer': 'zap',
  'redos-safety': 'shield',
  'validation-and-debugging': 'circleCheck',
  'match-tracing': 'route',
  'highlighting': 'highlighter',
  'diagrams': 'route',
  'backtracking-profiler': 'gauge',
  'regex-coverage': 'target',
  'test-data-generation': 'sparkles',
  'property-testing': 'flask',
  'xunit': 'flask',
  'regex-to-fluent-code': 'code',
  'learning-from-examples': 'sparkles',
  'ai-drafting': 'bot',
  'export': 'globe',
  'sql-dialects': 'database',
  'sql-like': 'database',
  'entity-framework-core': 'database',
  'aspnetcore': 'server',
  'compile-time-checks': 'code',
  'command-line-tool': 'terminal',
  'playground': 'play',
  'examples': 'rocket',
  'api-reference': 'tree',
  'best-practices': 'circleCheck',
  'regex-cheat-sheet': 'book',
  'upgrading-from-2x': 'arrowRight',
};

function CardBody({icon, title, description}) {
  return (
    <>
      <span className={styles.icon}>
        <Icon name={icon} size={18} />
      </span>
      <span className={styles.title}>
        {title}
        <Icon name="arrowRight" size={14} className={styles.arrow} />
      </span>
      {description && <span className={styles.description}>{description}</span>}
    </>
  );
}

function DocLinkCard({item}) {
  const doc = useDocById(item.docId ?? undefined);
  const description = item.description ?? doc?.description;
  return (
    <Link to={item.href} className={styles.card}>
      <CardBody icon={iconByDocId[item.docId] ?? 'regex'} title={item.label} description={description} />
    </Link>
  );
}

function CategoryCard({item}) {
  const href = item.href ?? item.items?.find((child) => child.href)?.href;
  if (!href) {
    return null;
  }
  const count = item.items?.length ?? 0;
  return (
    <Link to={href} className={styles.card}>
      <CardBody
        icon="layers"
        title={item.label}
        description={item.description ?? `${count} ${count === 1 ? 'page' : 'pages'}`}
      />
    </Link>
  );
}

function DocCardListForCurrentSidebarCategory({className}) {
  const items = useCurrentSidebarSiblings();
  return <DocCardList items={items} className={className} />;
}

/**
 * A grid of linked cards, one per sidebar item.
 */
export default function DocCardList(props) {
  const {items, className} = props;
  if (!items) {
    return <DocCardListForCurrentSidebarCategory {...props} />;
  }
  const filteredItems = filterDocCardListItems(items);
  return (
    <ul className={clsx(styles.grid, className)}>
      {filteredItems.map((item, index) => (
        <li key={index} className={styles.item}>
          {item.type === 'category' ? <CategoryCard item={item} /> : <DocLinkCard item={item} />}
        </li>
      ))}
    </ul>
  );
}
