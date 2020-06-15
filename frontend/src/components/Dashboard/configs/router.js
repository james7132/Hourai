import Overview from "./Overview.vue"
import Analytics from "./Analytics.vue"
import Logging from "./Logging.vue"
import Roles from "./Roles.vue"
import Music from "./Music.vue"
import Moderation from "./Moderation.vue"
import Validation from "./Validation.vue"

let categories = [{
    name: "General",
    entries: [{
        path: 'overview',
        name: 'overview',
        component: Overview
    }, {
        path: 'analytics',
        name: 'analytics',
        component: Analytics
    }]
}, {
    name: "Configuration",
    entries: [{
        path: 'validation',
        name: 'validation',
        component: Validation
    }, {
        path: 'logging',
        name: 'logging',
        component: Logging
    }, {
        path: 'roles',
        name: 'roles',
        component: Roles
    }, {
        path: 'music',
        name: 'music',
        component: Music
    }, {
        path: 'moderation',
        name: 'moderation',
        component: Moderation
    }]
}]

export default {
    categories: categories,
    getRouterEntries() {
        return categories.flatMap(c => c.entries)
    }
}
