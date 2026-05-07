<script setup lang="ts">
import type { Component } from 'vue'
import type { TenantRole } from '@/api/auth'
import { FileUp, FolderOpen, Search, ShieldCheck } from 'lucide-vue-next'
import { computed, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'
import { useAuth } from '@/lib/auth'
import { getRecentAssets } from '@/lib/recentAssets'

interface WorkspaceEntry {
  title: string
  description: string
  to: string
  icon: Component
  roles?: TenantRole[]
}

const router = useRouter()
const { state, canAccessRole } = useAuth()
const searchKeyword = ref('')

const displayName = computed(() => state.user?.displayName || state.user?.qqId || '用户')
const recentAssets = computed(() => getRecentAssets(state.currentTenant?.id))

const entries: WorkspaceEntry[] = [
  { title: '统一搜索', description: '搜索原文和已导入译文。', to: '/search', icon: Search },
  { title: '资产目录', description: '浏览剧情类型、剧情集和剧情。', to: '/assets', icon: FolderOpen },
  { title: '历史译文导入', description: '导入 JSON 译文版本。', to: '/import/translations', icon: FileUp },
  { title: '管理入口', description: '用户邀请、同步任务和索引维护。', to: '/admin/users', icon: ShieldCheck, roles: ['admin', 'super_admin'] },
]

const visibleEntries = computed(() => {
  return entries.filter(entry => canAccessRole(state.currentTenant?.role, entry.roles))
})

function submitSearch() {
  const keyword = searchKeyword.value.trim()
  router.push({
    path: '/search',
    query: keyword ? { keyword } : {},
  })
}
</script>

<template>
  <div class="space-y-6">
    <section class="rounded-lg border bg-card p-6">
      <div class="grid gap-5 lg:grid-cols-[1fr_380px] lg:items-end">
        <div>
          <p class="text-sm text-muted-foreground">
            语言资产工作台
          </p>
          <h1 class="mt-2 text-2xl font-semibold tracking-normal">
            {{ displayName }}，欢迎回到语言资产工作台
          </h1>
          <p class="mt-3 max-w-2xl text-sm leading-6 text-muted-foreground">
            在这里检索剧情文本、浏览资产目录、导入历史译文，并按权限管理成员和运维任务。
          </p>
        </div>
        <form class="flex gap-2" @submit.prevent="submitSearch">
          <div class="relative min-w-0 flex-1">
            <Search :size="18" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              v-model="searchKeyword"
              class="w-full rounded-md border bg-background py-2 pl-10 pr-3 text-sm outline-none focus:ring-2 focus:ring-ring"
              placeholder="搜索剧情原文或译文"
              type="search"
            >
          </div>
          <Button>搜索</Button>
        </form>
      </div>
    </section>

    <section class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
      <RouterLink v-for="entry in visibleEntries" :key="entry.to" :to="entry.to">
        <Card class="h-full transition hover:border-primary/60 hover:shadow-sm">
          <CardHeader>
            <component :is="entry.icon" :size="22" class="text-primary" />
            <CardTitle class="text-base">
              {{ entry.title }}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p class="text-sm leading-6 text-muted-foreground">
              {{ entry.description }}
            </p>
          </CardContent>
        </Card>
      </RouterLink>
    </section>

    <section class="space-y-3">
      <div class="flex items-center justify-between gap-3">
        <h2 class="text-base font-semibold">
          最近浏览
        </h2>
        <RouterLink class="text-sm text-primary hover:underline" to="/assets">
          打开资产目录
        </RouterLink>
      </div>
      <div v-if="recentAssets.length === 0" class="rounded-md border border-dashed bg-card p-6 text-sm text-muted-foreground">
        浏览剧情集或剧情后，这里会显示最近访问的资产。
      </div>
      <div v-else class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        <RouterLink
          v-for="asset in recentAssets"
          :key="`${asset.kind}-${asset.id}`"
          :to="asset.href"
          class="rounded-md border bg-card p-4 transition hover:border-primary/60 hover:shadow-sm"
        >
          <p class="text-xs text-muted-foreground">
            {{ asset.kind === 'group' ? '剧情集' : '剧情' }} · {{ asset.storyType }}
          </p>
          <p class="mt-2 font-medium">
            {{ asset.title }}
          </p>
          <p class="mt-1 truncate text-sm text-muted-foreground">
            {{ asset.subtitle || '无副标题' }}
          </p>
        </RouterLink>
      </div>
    </section>
  </div>
</template>
