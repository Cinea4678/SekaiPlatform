import type { TenantRole } from '@/api/auth'
import { createRouter, createWebHistory } from 'vue-router'
import MainLayout from '@/layouts/MainLayout.vue'
import { bootstrapSession, useAuth } from '@/lib/auth'

interface RouteAccessMeta {
  title?: string
  description?: string
  requiresAuth?: boolean
  requiresTenant?: boolean
  roles?: TenantRole[]
}

declare module 'vue-router' {
  interface RouteMeta extends RouteAccessMeta {}
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'Login',
      component: () => import('@/views/auth/LoginView.vue'),
    },
    {
      path: '/tenant/select',
      name: 'TenantSelect',
      component: () => import('@/views/auth/TenantSelectView.vue'),
      meta: {
        requiresAuth: true,
      },
    },
    {
      path: '/',
      component: MainLayout,
      meta: {
        requiresAuth: true,
        requiresTenant: true,
      },
      children: [
        {
          path: '',
          name: 'Workspace',
          component: () => import('@/views/WorkspaceView.vue'),
        },
        {
          path: 'search',
          name: 'Search',
          component: () => import('@/views/search/SearchView.vue'),
          meta: {
            title: '统一搜索',
            description: '如果不确定怎么翻译的话，不妨搜索一下之前的翻译吧。',
          },
        },
        {
          path: 'assets',
          name: 'Assets',
          component: () => import('@/views/assets/AssetsHomeView.vue'),
          meta: {
            title: '资产目录',
            description: '按剧情类型、剧情集和剧情组织语言资产，方便持续查阅。',
          },
        },
        {
          path: 'assets/groups',
          name: 'StoryGroups',
          component: () => import('@/views/assets/StoryGroupsView.vue'),
          meta: {
            title: '剧情集',
            description: '按剧情类型、关键词和分页浏览剧情集。',
          },
        },
        {
          path: 'assets/groups/:storyGroupId',
          name: 'StoryGroupDetail',
          component: () => import('@/views/assets/StoryGroupDetailView.vue'),
          meta: {
            title: '剧情集详情',
            description: '查看剧情集信息和该剧情集下的剧情。',
          },
        },
        {
          path: 'stories',
          name: 'Stories',
          component: () => import('@/views/assets/StoriesView.vue'),
          meta: {
            title: '全部剧情',
            description: '按剧情集、剧情类型、关键词和分页浏览剧情。',
          },
        },
        {
          path: 'stories/:storyId',
          name: 'StoryDetail',
          component: () => import('@/views/assets/StoryDetailView.vue'),
          meta: {
            title: '剧情详情',
            description: '查看剧情元信息和资产归属。',
          },
        },
        {
          path: 'translations/:translationVersionId',
          name: 'TranslationVersionDetail',
          component: () => import('@/views/assets/TranslationVersionView.vue'),
          meta: {
            title: '译文版本',
            description: '打开译文版本对应的剧情阅读页。',
          },
        },
        {
          path: 'import/translations',
          name: 'ImportTranslations',
          component: () => import('@/views/PlaceholderView.vue'),
          meta: {
            title: '历史译文导入',
            description: '导入历史 JSON 译文，沉淀为当前租户下的新翻译版本。',
          },
        },
        {
          path: 'admin/users',
          name: 'AdminUsers',
          component: () => import('@/views/PlaceholderView.vue'),
          meta: {
            title: '租户用户',
            description: '邀请成员加入当前租户，并按职责授予访问权限。',
            roles: ['admin', 'super_admin'],
          },
        },
        {
          path: 'admin/sync',
          name: 'AdminSync',
          component: () => import('@/views/PlaceholderView.vue'),
          meta: {
            title: '同步任务',
            description: '触发外部原文同步，并跟踪同步任务的执行状态。',
            roles: ['admin', 'super_admin'],
          },
        },
        {
          path: 'admin/search-index',
          name: 'AdminSearchIndex',
          component: () => import('@/views/PlaceholderView.vue'),
          meta: {
            title: '搜索索引维护',
            description: '维护搜索索引，让最新原文和译文可被稳定检索。',
            roles: ['super_admin'],
          },
        },
      ],
    },
  ],
})

router.beforeEach(async (to) => {
  const { state, canAccessRole } = useAuth()
  await bootstrapSession()

  if (to.name === 'Login') {
    if (!state.user) {
      return true
    }

    return state.currentTenant ? '/' : '/tenant/select'
  }

  if (to.meta.requiresAuth && !state.user) {
    return {
      path: '/login',
      query: { redirect: to.fullPath },
    }
  }

  if (to.meta.requiresTenant && !state.currentTenant) {
    return '/tenant/select'
  }

  if (!canAccessRole(state.currentTenant?.role, to.meta.roles)) {
    return '/'
  }

  return true
})

export default router
