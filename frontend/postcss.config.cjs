// PostCSS runs over every CSS file Vite loads (https://mantine.dev/styles/postcss-preset/).
// - postcss-preset-mantine: Mantine helpers for our own CSS – rem()/em() functions and mixins such
//   as @mixin dark / @mixin smaller-than.
// - postcss-simple-vars: lets CSS files use $mantine-breakpoint-sm etc. in media queries, with the
//   same values Mantine uses for its responsive props (hiddenFrom="sm", visibleFrom="sm"...).
// .cjs because package.json has "type": "module" and this file uses CommonJS (module.exports).
module.exports = {
  plugins: {
    "postcss-preset-mantine": {},
    "postcss-simple-vars": {
      variables: {
        "mantine-breakpoint-xs": "36em",
        "mantine-breakpoint-sm": "48em",
        "mantine-breakpoint-md": "62em",
        "mantine-breakpoint-lg": "75em",
        "mantine-breakpoint-xl": "88em",
      },
    },
  },
};
