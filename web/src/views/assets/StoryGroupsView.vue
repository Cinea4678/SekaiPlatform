<script setup lang="ts">
import type { StoryGroup, StoryTypeInfo } from '@/api/assets'
import type { PagedResponse } from '@/api/types'
import { FolderOpen, Search } from 'lucide-vue-next'
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { getStoryGroups, getStoryTypes } from '@/api/assets'
import { ApiError } from '@/api/client'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PaginationControls from '@/components/PaginationControls.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import { createStoryTypeLabeler, formatDateTime } from '@/lib/display'
import { defaultPageSize, readQueryNumber, readQueryString } from '@/lib/query'

const route = useRoute()
const router = useRouter()
const storyTypes = ref<StoryTypeInfo[]>([])
const storyGroups = ref<PagedResponse<StoryGroup> | null>(null)
const loading = ref(true)
const error = ref<ApiError | null>(null)
const keyword = ref('')
const storyType = ref('')
const storyTypeLabel = computed(() => createStoryTypeLabeler(storyTypes.value))

onMounted(loadPage)
watch(() => route.fullPath, loadPage)

async function loadPage() {
  loading.value = true
  error.value = null
  keyword.value = readQueryString(route.query.keyword)
  storyType.value = readQueryString(route.query.story_type)

  try {
    const page = readQueryNumber(route.query.page, 1)
    const pageSize = readQueryNumber(route.query.page_size, defaultPageSize)
    const [types, groups] = await Promise.all([
      storyTypes.value.length ? Promise.resolve(storyTypes.value) : getStoryTypes(),
      getStoryGroups({
        storyType: storyType.value || undefined,
        keyword: keyword.value || undefined,
        page,
        pageSize,
      }),
    ])
    storyTypes.value = types
    storyGroups.value = groups
  }
  catch (loadError) {
    error.value = loadError instanceof ApiError
      ? loadError
      : new ApiError(0, '剧情集加载失败，请稍后重试。')
  }
  finally {
    loading.value = false
  }
}

function submitFilters() {
  router.push({
    path: '/assets/groups',
    query: {
      ...(storyType.value ? { story_type: storyType.value } : {}),
      ...(keyword.value.trim() ? { keyword: keyword.value.trim() } : {}),
      page: '1',
      page_size: String(defaultPageSize),
    },
  })
}

function changePage(page: number) {
  router.push({
    path: '/assets/groups',
    query: {
      ...route.query,
      page: String(page),
      page_size: String(storyGroups.value?.page.pageSize || defaultPageSize),
    },
  })
}
</script>

<template>
  <div class="space-y-6">
    <div class="space-y-2">
      <div class="flex items-center gap-2 text-sm text-muted-foreground">
        <RouterLink class="hover:text-foreground" to="/assets">
          资产目录
        </RouterLink>
        <span>/</span>
        <span>剧情集</span>
      </div>
      <h1 class="text-2xl font-semibold tracking-normal">
        剧情集
      </h1>
    </div>

    <form class="grid gap-3 rounded-lg border bg-card p-4 md:grid-cols-[220px_1fr_auto]" @submit.prevent="submitFilters">
      <select v-model="storyType" class="rounded-md border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring">
        <option value="">
          全部类型
        </option>
        <option v-for="item in storyTypes" :key="item.value" :value="item.value">
          {{ item.label }}
        </option>
      </select>
      <div class="relative">
        <Search :size="18" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
        <input
          v-model="keyword"
          class="w-full rounded-md border bg-background py-2 pl-10 pr-3 text-sm outline-none focus:ring-2 focus:ring-ring"
          placeholder="搜索标题、副标题或外部 ID"
          type="search"
        >
      </div>
      <Button>筛选</Button>
    </form>

    <LoadingState v-if="loading" label="加载剧情集" />
    <ErrorState v-else-if="error" :error="error" @retry="loadPage" />
    <EmptyState
      v-else-if="!storyGroups || storyGroups.items.length === 0"
      title="没有匹配的剧情集"
      description="调整剧情类型或关键词后再试。"
    />
    <Card v-else>
      <CardContent class="space-y-0 p-0">
        <RouterLink
          v-for="group in storyGroups.items"
          :key="group.id"
          :to="`/assets/groups/${group.id}`"
          class="flex flex-col gap-3 border-b p-4 transition last:border-b-0 hover:bg-accent/60 md:flex-row md:items-center md:justify-between"
        >
          <div class="min-w-0">
            <div class="flex flex-wrap items-center gap-2">
              <FolderOpen :size="18" class="text-primary" />
              <p class="font-medium">
                {{ group.title }}
              </p>
              <span class="rounded-md bg-secondary px-2 py-1 text-xs text-secondary-foreground">
                {{ storyTypeLabel(group.storyType) }}
              </span>
            </div>
            <p class="mt-2 text-sm text-muted-foreground">
              {{ group.subtitle || '无副标题' }}
            </p>
            <p class="mt-2 text-xs text-muted-foreground">
              编号：{{ group.displayNo ?? '-' }} · 外部 ID：{{ group.externalId || '-' }}
            </p>
          </div>
          <p class="shrink-0 text-xs text-muted-foreground">
            更新于 {{ formatDateTime(group.updatedAt) }}
          </p>
        </RouterLink>
      </CardContent>
      <div class="p-4">
        <PaginationControls :page="storyGroups.page" @change="changePage" />
      </div>
    </Card>
  </div>
</template>
