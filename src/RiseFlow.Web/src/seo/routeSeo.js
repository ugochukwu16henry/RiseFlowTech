const SITE_NAME = 'RiseFlow';
const DEFAULT_TITLE = 'RiseFlow - School Management Platform for African Schools';
const DEFAULT_DESCRIPTION = 'RiseFlow helps schools manage student records, fees, reports, communication, and transcript verification from one secure platform.';
const DEFAULT_IMAGE = '/media/hero-icon-4-multi-tenant-platform.png';

const routes = [
  {
    test: (pathname) => pathname === '/',
    title: 'RiseFlow - School Management Platform for Modern Schools',
    description: 'Manage school operations, student records, results, fees, and parent communication from one platform.',
    keywords: 'school management software, student records, school fees platform, result management',
    type: 'website',
    index: true,
    schema: 'home',
  },
  {
    test: (pathname) => pathname === '/onboard',
    title: 'Onboard Your School | RiseFlow',
    description: 'Create your school workspace on RiseFlow and set up your team, classes, and students quickly.',
    keywords: 'school onboarding, school registration software',
    type: 'website',
    index: true,
    schema: 'service',
  },
  {
    test: (pathname) => pathname === '/affiliate-program',
    title: 'Affiliate Program | RiseFlow',
    description: 'Join the RiseFlow affiliate program and earn by helping schools adopt modern school operations.',
    keywords: 'affiliate program, school SaaS affiliate',
    type: 'website',
    index: true,
    schema: 'service',
  },
  {
    test: (pathname) => pathname === '/terms',
    title: 'Terms of Service | RiseFlow',
    description: 'Read RiseFlow terms of service for schools, teachers, parents, and platform partners.',
    keywords: 'terms of service',
    type: 'article',
    index: true,
    schema: 'legal',
  },
  {
    test: (pathname) => pathname === '/privacy',
    title: 'Privacy Policy | RiseFlow',
    description: 'Read how RiseFlow protects school, teacher, parent, and student data privacy.',
    keywords: 'privacy policy, student data privacy',
    type: 'article',
    index: true,
    schema: 'legal',
  },
  {
    test: (pathname) => pathname.startsWith('/verify/transcript/'),
    title: 'Verify Student Transcript | RiseFlow',
    description: 'Validate transcript authenticity with RiseFlow secure verification.',
    keywords: 'transcript verification, student transcript validation',
    type: 'website',
    index: true,
    schema: 'verify',
  },
];

function buildSchema(schemaType, absoluteUrl) {
  if (schemaType === 'home') {
    return [
      {
        '@context': 'https://schema.org',
        '@type': 'Organization',
        name: SITE_NAME,
        url: absoluteUrl,
        logo: `${absoluteUrl}/favicon-96x96.png`,
        sameAs: [],
      },
      {
        '@context': 'https://schema.org',
        '@type': 'WebSite',
        name: SITE_NAME,
        url: absoluteUrl,
        potentialAction: {
          '@type': 'SearchAction',
          target: `${absoluteUrl}/?q={search_term_string}`,
          'query-input': 'required name=search_term_string',
        },
      },
    ];
  }

  if (schemaType === 'service') {
    return [
      {
        '@context': 'https://schema.org',
        '@type': 'Service',
        name: `${SITE_NAME} School Management Platform`,
        provider: {
          '@type': 'Organization',
          name: SITE_NAME,
          url: absoluteUrl,
        },
        areaServed: 'Africa',
        serviceType: 'School Management Software',
      },
    ];
  }

  if (schemaType === 'legal') {
    return [
      {
        '@context': 'https://schema.org',
        '@type': 'WebPage',
        name: 'Legal Information',
        isPartOf: {
          '@type': 'WebSite',
          name: SITE_NAME,
          url: absoluteUrl,
        },
      },
    ];
  }

  if (schemaType === 'verify') {
    return [
      {
        '@context': 'https://schema.org',
        '@type': 'WebApplication',
        name: `${SITE_NAME} Transcript Verification`,
        applicationCategory: 'EducationalApplication',
        operatingSystem: 'Web',
        url: absoluteUrl,
      },
    ];
  }

  return [];
}

export function getRouteSeo(pathname, absoluteUrl) {
  const matched = routes.find((route) => route.test(pathname));

  if (!matched) {
    return {
      title: `${SITE_NAME} Dashboard`,
      description: 'Secure school dashboard for admins, teachers, students, and parents.',
      keywords: 'school dashboard',
      image: DEFAULT_IMAGE,
      canonicalPath: pathname,
      type: 'website',
      index: false,
      schema: [],
    };
  }

  return {
    title: matched.title ?? DEFAULT_TITLE,
    description: matched.description ?? DEFAULT_DESCRIPTION,
    keywords: matched.keywords ?? 'school management platform',
    image: matched.image ?? DEFAULT_IMAGE,
    canonicalPath: pathname,
    type: matched.type ?? 'website',
    index: matched.index ?? true,
    schema: buildSchema(matched.schema, absoluteUrl),
  };
}

export const seoDefaults = {
  siteName: SITE_NAME,
  title: DEFAULT_TITLE,
  description: DEFAULT_DESCRIPTION,
  image: DEFAULT_IMAGE,
};
