<script setup lang="ts">
import { FileUp, FolderOpen, Search, ShieldCheck } from 'lucide-vue-next'
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'
import { useAuth } from '@/lib/auth'

const { state } = useAuth()

const displayName = computed(() => state.user?.displayName || state.user?.qqId || '用户')
const roleLabel = computed(() => {
  if (state.currentTenant?.role === 'super_admin') {
    return '超级管理员'
  }

  if (state.currentTenant?.role === 'admin') {
    return '管理员'
  }

  return '成员'
})

const entries = [
  { title: '统一搜索', description: '搜索原文和当前租户译文。', to: '/search', icon: Search },
  { title: '资产目录', description: '浏览剧情类型、剧情集和剧情。', to: '/assets', icon: FolderOpen },
  { title: '历史译文导入', description: '导入 JSON 译文版本。', to: '/import/translations', icon: FileUp },
  { title: '管理入口', description: '用户邀请、同步任务和索引维护。', to: '/admin/users', icon: ShieldCheck },
]
</script>

<template>
  <div class="space-y-6">
    <section class="rounded-lg border bg-card p-6">
      <p class="text-sm text-muted-foreground">
        {{ state.currentTenant?.name }} · {{ roleLabel }}
      </p>
      <h1 class="mt-2 text-2xl font-semibold tracking-normal">
        {{ displayName }}，欢迎回到语言资产工作台
      </h1>
      <p class="mt-3 max-w-2xl text-sm leading-6 text-muted-foreground">
        在这里检索剧情文本、浏览资产目录、导入历史译文，并按权限管理租户成员和运维任务。
      </p>
    </section>

    <section class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
      <RouterLink v-for="entry in entries" :key="entry.to" :to="entry.to">
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
  </div>
</template>
