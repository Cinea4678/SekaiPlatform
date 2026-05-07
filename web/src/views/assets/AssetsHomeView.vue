<script setup lang="ts">
import type { StoryTypeInfo } from '@/api/assets'
import { BookOpen, FolderOpen, Languages, Search } from 'lucide-vue-next'
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { getStoryTypes } from '@/api/assets'
import { ApiError } from '@/api/client'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import LoadingState from '@/components/LoadingState.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'
import { useAuth } from '@/lib/auth'
import { formatDateTime } from '@/lib/display'
import { getRecentAssets } from '@/lib/recentAssets'

const router = useRouter()
const { state } = useAuth()
const storyTypes = ref<StoryTypeInfo[]>([])
const keyword = ref('')
const loading = ref(true)
const error = ref<ApiError | null>(null)
const recentAssets = computed(() => getRecentAssets(state.currentTenant?.id))

onMounted(loadStoryTypes)

async function loadStoryTypes() {
  loading.value = true
  error.value = null

  try {
    storyTypes.value = await getStoryTypes()
  }
  catch (loadError) {
    error.value = loadError instanceof ApiError
      ? loadError
      : new ApiError(0, '资产目录加载失败，请稍后重试。')
  }
  finally {
    loading.value = false
  }
}

function submitKeyword() {
  const text = keyword.value.trim()
  router.push({
    path: '/stories',
    query: text ? { keyword: text, page: '1', page_size: '20' } : { page: '1', page_size: '20' },
  })
}
</script>

<template>
  <div class="space-y-6">
    <section class="rounded-lg border bg-card p-6">
      <p class="text-sm text-muted-foreground">
        资产目录
      </p>
      <div class="mt-4 grid gap-5 lg:grid-cols-[1fr_360px] lg:items-end">
        <div>
          <h1 class="text-2xl font-semibold tracking-normal">
            浏览剧情资产
          </h1>
          <p class="mt-3 max-w-2xl text-sm leading-6 text-muted-foreground">
            按剧情类型、剧情集和剧情编号定位共享原文，为后续搜索、导入和译文阅读提供入口。
          </p>
        </div>
        <form class="flex gap-2" @submit.prevent="submitKeyword">
          <div class="relative min-w-0 flex-1">
            <Search :size="18" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              v-model="keyword"
              class="w-full rounded-md border bg-background py-2 pl-10 pr-3 text-sm outline-none focus:ring-2 focus:ring-ring"
              placeholder="搜索剧情标题或 scenario_id"
              type="search"
            >
          </div>
          <Button>搜索</Button>
        </form>
      </div>
    </section>

    <LoadingState v-if="loading" label="加载剧情类型" />
    <ErrorState v-else-if="error" :error="error" @retry="loadStoryTypes" />

    <template v-else>
      <section class="grid gap-4 md:grid-cols-3">
        <RouterLink to="/assets/groups?page=1&page_size=20">
          <Card class="h-full transition hover:border-primary/60 hover:shadow-sm">
            <CardHeader>
              <FolderOpen :size="22" class="text-primary" />
              <CardTitle class="text-base">
                剧情集
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p class="text-sm leading-6 text-muted-foreground">
                按活动、卡面、主线等类型浏览剧情集合。
              </p>
            </CardContent>
          </Card>
        </RouterLink>

        <RouterLink to="/stories?page=1&page_size=20">
          <Card class="h-full transition hover:border-primary/60 hover:shadow-sm">
            <CardHeader>
              <BookOpen :size="22" class="text-primary" />
              <CardTitle class="text-base">
                全部剧情
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p class="text-sm leading-6 text-muted-foreground">
                直接按标题、剧情编号和类型查找具体剧情。
              </p>
            </CardContent>
          </Card>
        </RouterLink>

        <RouterLink to="/stories?has_translation=true&page=1&page_size=20">
          <Card class="h-full transition hover:border-primary/60 hover:shadow-sm">
            <CardHeader>
              <Languages :size="22" class="text-primary" />
              <CardTitle class="text-base">
                有译文剧情
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p class="text-sm leading-6 text-muted-foreground">
                查看已导入译文版本的剧情。
              </p>
            </CardContent>
          </Card>
        </RouterLink>
      </section>

      <section class="space-y-3">
        <div class="flex items-center justify-between gap-3">
          <h2 class="text-base font-semibold">
            剧情类型
          </h2>
          <RouterLink class="text-sm text-primary hover:underline" to="/stories?page=1&page_size=20">
            查看全部剧情
          </RouterLink>
        </div>

        <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
          <RouterLink
            v-for="storyType in storyTypes"
            :key="storyType.value"
            :to="{ path: '/assets/groups', query: { story_type: storyType.value, page: '1', page_size: '20' } }"
            class="rounded-md border bg-card p-4 transition hover:border-primary/60 hover:shadow-sm"
          >
            <p class="font-medium">
              {{ storyType.label }}
            </p>
            <p class="mt-2 text-xs text-muted-foreground">
              {{ storyType.value }}
            </p>
          </RouterLink>
        </div>
      </section>

      <section class="space-y-3">
        <h2 class="text-base font-semibold">
          最近浏览
        </h2>
        <EmptyState
          v-if="recentAssets.length === 0"
          title="暂无浏览记录"
          description="打开剧情集或剧情详情后，这里会显示最近访问的资产。"
        />
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
            <p class="mt-3 text-xs text-muted-foreground">
              {{ formatDateTime(asset.viewedAt) }}
            </p>
          </RouterLink>
        </div>
      </section>
    </template>
  </div>
</template>
