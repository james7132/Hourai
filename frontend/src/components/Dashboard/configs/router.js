//import Overview from "./Overview.vue"
//import Analytics from "./Analytics.vue"
//import Logging from "./Logging.vue"
//import Roles from "./Roles.vue"
//import Music from "./Music.vue"
//import Moderation from "./Moderation.vue"
//import Validation from "./Validation.vue"

import UnderConstruction from '@/components/common/UnderConstruction.vue'

let categories = [{
    name: "General",
    entries: [{
        path: 'overview',
        name: 'overview',
        component: UnderConstruction
    }, {
        path: 'analytics',
        name: 'analytics',
        component: UnderConstruction
    }]
}, {
    name: "Configuration",
    entries: [{
        path: 'validation',
        name: 'validation',
        component: UnderConstruction
    }, {
        path: 'logging',
        name: 'logging',
        component: UnderConstruction
    }, {
        path: 'roles',
        name: 'roles',
        component: UnderConstruction
    }, {
        path: 'music',
        name: 'music',
        component: UnderConstruction
    }, {
        path: 'moderation',
        name: 'moderation',
        component: UnderConstruction
    }]
}]

export default {
    categories: categories,
    getRouterEntries() {
        return categories.flatMap(c => c.entries)
    }
}
